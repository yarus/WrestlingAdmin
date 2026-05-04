using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;

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

        private readonly object _stateLock = new object();
        private UdpClient _listener;
        private Thread _receiveThread;
        private Timer _announceTimer;
        private Timer _expiryTimer;
        private volatile bool _running;
        private int _activePort;
        private PeerAdvertisement _currentAd;
        private Func<string> _stateHashProvider;

        public event EventHandler<DiscoveredPeer> PeerUpserted;
        public event EventHandler<DiscoveredPeer> PeerExpired;
        public event EventHandler<string> DiagnosticMessage;

        public PeerDiscoveryService()
            : this(clock: null, announceInterval: null, expiry: null, appVersion: null)
        {
        }

        public PeerDiscoveryService(Func<DateTime> clock = null, TimeSpan? announceInterval = null, TimeSpan? expiry = null, string appVersion = null)
        {
            _clock = clock ?? (() => DateTime.UtcNow);
            _announceInterval = announceInterval ?? DefaultAnnounceInterval;
            _appVersion = appVersion ?? "1.0.0";
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
                HttpUrl = httpUrl ?? string.Empty,
                AppVersion = _appVersion,
                StateHash = string.Empty
            };

            lock (_stateLock)
            {
                _currentAd = ad;
                _stateHashProvider = stateHashProvider;
                _activePort = port;
                _registry.SetContext(_instanceId, tournamentId);

                try
                {
                    _listener = new UdpClient(new IPEndPoint(IPAddress.Any, port));
                    _listener.EnableBroadcast = true;
                }
                catch (Exception ex)
                {
                    DiagnosticMessage?.Invoke(this, "Не удалось открыть UDP порт " + port + ": " + ex.Message);
                    try { _listener?.Close(); } catch { }
                    _listener = null;
                    return;
                }

                _running = true;
                _receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "PeerDiscovery.Receive" };
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

        private void ReceiveLoop()
        {
            // Capture a local reference so Stop()-ing nil'ing _listener doesn't
            // NPE us between iterations.
            var listener = _listener;
            while (_running && listener != null)
            {
                IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                byte[] data;
                try
                {
                    data = listener.Receive(ref remote);
                }
                catch (SocketException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    DiagnosticMessage?.Invoke(this, "Ошибка приёма UDP: " + ex.Message);
                    continue;
                }

                var ad = PeerAdvertisement.TryFromBytes(data);
                if (ad == null) continue;
                _registry.Ingest(ad, remote.Address, _clock());
            }
        }

        private void AnnounceSafely()
        {
            PeerAdvertisement ad;
            Func<string> hashProvider;
            lock (_stateLock) { ad = _currentAd; hashProvider = _stateHashProvider; }
            if (ad == null) return;

            ad.SentAt = _clock();
            if (hashProvider != null)
            {
                try { ad.StateHash = hashProvider() ?? string.Empty; }
                catch { ad.StateHash = string.Empty; }
            }
            byte[] bytes;
            try { bytes = ad.ToBytes(); }
            catch (Exception ex)
            {
                DiagnosticMessage?.Invoke(this, "Ошибка сериализации анонса: " + ex.Message);
                return;
            }

            int port;
            lock (_stateLock) { port = _activePort; }
            if (port <= 0) return;

            foreach (var broadcast in EnumerateBroadcastAddresses())
            {
                try
                {
                    using (var sender = new UdpClient())
                    {
                        sender.EnableBroadcast = true;
                        sender.Send(bytes, bytes.Length, new IPEndPoint(broadcast, port));
                    }
                }
                catch
                {
                    // One flaky interface should not block the rest. Next tick
                    // will retry — announcements are idempotent.
                }
            }
        }

        private static IEnumerable<IPAddress> EnumerateBroadcastAddresses()
        {
            // Limited broadcast always — reaches peers on the default
            // interface even when we can't enumerate NICs.
            yield return IPAddress.Broadcast;

            NetworkInterface[] nics;
            try
            {
                nics = NetworkInterface.GetAllNetworkInterfaces();
            }
            catch
            {
                yield break;
            }

            foreach (var nic in nics)
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                IPInterfaceProperties props;
                try { props = nic.GetIPProperties(); }
                catch { continue; }

                foreach (var unicast in props.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var mask = unicast.IPv4Mask;
                    if (mask == null) continue;
                    var addrBytes = unicast.Address.GetAddressBytes();
                    var maskBytes = mask.GetAddressBytes();
                    if (addrBytes.Length != 4 || maskBytes.Length != 4) continue;
                    var broadcastBytes = new byte[4];
                    for (int i = 0; i < 4; i++)
                    {
                        broadcastBytes[i] = (byte)(addrBytes[i] | (~maskBytes[i] & 0xFF));
                    }
                    yield return new IPAddress(broadcastBytes);
                }
            }
        }
    }
}
