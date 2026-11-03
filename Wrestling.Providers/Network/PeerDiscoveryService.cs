using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Wrestling.Providers.Localization;

namespace Wrestling.Providers.Network
{
    // Production glue for UDP peer discovery: owns a listener socket, a
    // background receive thread, an announce timer, and an expiry timer.
    // Aggregation logic lives in PeerRegistry — this class just moves bytes
    // in and out of sockets. Safe to call Start/Stop repeatedly.
    public sealed class PeerDiscoveryService : IPeerDiscoveryService
    {
        private static readonly TimeSpan DefaultAnnounceInterval = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan DefaultExpiry = TimeSpan.FromSeconds(15);
        private static readonly TimeSpan DefaultExpiryTick = TimeSpan.FromSeconds(1);

        private readonly Guid _instanceId = Guid.NewGuid();
        private readonly PeerRegistry _registry;
        private readonly Func<DateTime> _clock;
        private readonly TimeSpan _announceInterval;
        private readonly string _appVersion;
        private readonly Func<IReadOnlyList<LocalIpAddressProbe.LanAddress>> _addresses;

        private readonly object _stateLock = new object();
        private UdpClient _listener;
        private Thread _receiveThread;
        private Timer _announceTimer;
        private Timer _expiryTimer;
        private volatile bool _running;
        private int _activePort;
        private int _firstPort;
        private PeerAdvertisement _currentAd;
        private Func<string> _stateHashProvider;

        public event EventHandler<DiscoveredPeer> PeerUpserted;
        public event EventHandler<DiscoveredPeer> PeerExpired;
        public event EventHandler<string> DiagnosticMessage;

        public int? ActualPort { get { lock (_stateLock) return _running ? _activePort : null; } }

        public PeerDiscoveryService()
            : this(clock: null, announceInterval: null, expiry: null, appVersion: null)
        {
        }

        public PeerDiscoveryService(Func<DateTime> clock = null, TimeSpan? announceInterval = null, TimeSpan? expiry = null,
            string appVersion = null, Func<IReadOnlyList<LocalIpAddressProbe.LanAddress>> addresses = null)
        {
            _clock = clock ?? (() => DateTime.UtcNow);
            _announceInterval = announceInterval ?? DefaultAnnounceInterval;
            _appVersion = appVersion ?? "1.0.0";
            _addresses = addresses ?? LocalIpAddressProbe.EnumerateLanInterfaces;
            _registry = new PeerRegistry(expiry ?? DefaultExpiry);
            _registry.PeerUpserted += (s, p) => PeerUpserted?.Invoke(this, p);
            _registry.PeerExpired += (s, p) => PeerExpired?.Invoke(this, p);
        }

        public IReadOnlyCollection<DiscoveredPeer> SnapshotPeers()
        {
            return _registry.Snapshot();
        }

        public void StartForTournament(int port, Guid tournamentId, string tournamentTitle, string nodeName, string httpUrl, Func<string> stateHashProvider = null)
        {
            Stop();

            var ad = new PeerAdvertisement
            {
                Proto = PeerAdvertisement.CurrentProto,
                InstanceId = _instanceId,
                TournamentId = tournamentId,
                TournamentTitle = tournamentTitle ?? string.Empty,
                NodeName = nodeName ?? string.Empty,
                ComputerName = Environment.MachineName,
                HttpUrl = httpUrl ?? string.Empty,
                AppVersion = _appVersion,
                StateHash = string.Empty
            };

            lock (_stateLock)
            {
                _currentAd = ad;
                _stateHashProvider = stateHashProvider;
                _firstPort = port;
                _registry.SetContext(_instanceId, tournamentId);
                Exception failure = null;
                foreach (var candidate in DiscoveryPorts(port))
                {
                    var listener = new UdpClient(AddressFamily.InterNetwork);
                    try
                    {
                        listener.ExclusiveAddressUse = true;
                        listener.Client.Bind(new IPEndPoint(IPAddress.Any, candidate));
                        listener.EnableBroadcast = true;
                        _listener = listener;
                        _activePort = candidate;
                        break;
                    }
                    catch (SocketException ex) { failure = ex; listener.Dispose(); }
                }
                if (_listener == null)
                {
                    DiagnosticMessage?.Invoke(this, string.Format(ProviderLocalization.T("Network_UdpRangeUnavailable",
                        "Автоматическая синхронизация недоступна: не удалось открыть порты обнаружения UDP {0}–{1}. Они заняты или недоступны. {2}"),
                        port, Math.Min(65535, port + NetworkDefaults.DiscoveryPortCount - 1), failure?.Message));
                    return;
                }

                _running = true;
                var activeListener = _listener;
                _receiveThread = new Thread(() => ReceiveLoop(activeListener)) { IsBackground = true, Name = "PeerDiscovery.Receive" };
                _receiveThread.Start();

                _announceTimer = new Timer(_ => AnnounceSafely(), null, TimeSpan.Zero, _announceInterval);
                _expiryTimer = new Timer(_ => _registry.Tick(_clock()), null, DefaultExpiryTick, DefaultExpiryTick);
            }
        }

        public void Stop()
        {
            lock (_stateLock)
            {
                _running = false;
                _announceTimer?.Dispose();
                _announceTimer = null;
                _expiryTimer?.Dispose();
                _expiryTimer = null;
                try { _listener?.Close(); } catch { }
                _listener = null;
                _currentAd = null;
                _stateHashProvider = null;
                _activePort = 0;
            }
            _registry.Clear();
        }

        public void Dispose()
        {
            Stop();
        }

        private void ReceiveLoop(UdpClient listener)
        {
            while (IsCurrent(listener))
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] data;
                try
                {
                    data = listener.Receive(ref remote);
                }
                catch (SocketException ex)
                {
                    if (IsCurrent(listener))
                    {
                        lock (_stateLock) { if (ReferenceEquals(_listener, listener)) _running = false; }
                        DiagnosticMessage?.Invoke(this, ProviderLocalization.T("Network_UdpReceiveError", "Ошибка приёма UDP: ") + ex.Message);
                    }
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    DiagnosticMessage?.Invoke(this, ProviderLocalization.T("Network_UdpReceiveError", "Ошибка приёма UDP: ") + ex.Message);
                    continue;
                }

                var ad = PeerAdvertisement.TryFromBytes(data);
                if (ad == null || !IsCurrent(listener)) continue;
                _registry.Ingest(ad, remote.Address, _clock());
            }
        }

        private void AnnounceSafely()
        {
            PeerAdvertisement ad;
            Func<string> hashProvider;
            lock (_stateLock) { ad = _currentAd; hashProvider = _stateHashProvider; }
            if (ad == null) return;

            var hash = string.Empty;
            if (hashProvider != null)
            {
                try { hash = hashProvider() ?? string.Empty; }
                catch { hash = string.Empty; }
            }
            int port;
            lock (_stateLock)
            {
                if (!_running || !ReferenceEquals(ad, _currentAd)) return;
                port = _firstPort;
            }
            if (port <= 0) return;

            foreach (var address in _addresses())
            {
                try
                {
                    var packet = new PeerAdvertisement
                    {
                        Proto = ad.Proto, InstanceId = ad.InstanceId, TournamentId = ad.TournamentId,
                        TournamentTitle = ad.TournamentTitle, NodeName = ad.NodeName, ComputerName = ad.ComputerName,
                        HttpUrl = HttpUrlForAddress(ad.HttpUrl, address.Address), AppVersion = ad.AppVersion,
                        StateHash = hash, SentAt = _clock()
                    };
                    var bytes = packet.ToBytes();
                    using var sender = new UdpClient(new IPEndPoint(address.Address, 0));
                    sender.EnableBroadcast = true;
                    foreach (var destination in DiscoveryPorts(port))
                    {
                        lock (_stateLock) { if (!_running || !ReferenceEquals(ad, _currentAd)) return; }
                        try { sender.Send(bytes, bytes.Length, new IPEndPoint(address.BroadcastAddress, destination)); }
                        catch (SocketException) { /* Try the remaining discovery ports on this interface. */ }
                    }
                }
                catch (SocketException)
                {
                    // One flaky interface should not block the rest. Next tick
                    // will retry — announcements are idempotent.
                }
            }
        }

        private bool IsCurrent(UdpClient listener)
        {
            lock (_stateLock) return _running && ReferenceEquals(_listener, listener);
        }

        private static IEnumerable<int> DiscoveryPorts(int first)
        {
            for (var i = 0; i < NetworkDefaults.DiscoveryPortCount && first + i <= 65535; i++)
                if (first + i > 0) yield return first + i;
        }

        internal static string HttpUrlForAddress(string url, IPAddress address)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed) || parsed.Scheme != Uri.UriSchemeHttp) return string.Empty;
            return new UriBuilder(parsed) { Host = address.ToString() }.Uri.AbsoluteUri;
        }
    }
}
