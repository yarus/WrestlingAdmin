using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Wrestling.Providers.Localization;

namespace Wrestling.Providers.Network
{
    // Minimal TcpListener-based HTTP server that serves the currently-served
    // tournament .wrt by GUID-matched URL. HttpListener is avoided here because
    // it requires URL-ACL reservations (netsh http add urlacl) on non-admin
    // accounts — operators run without admin rights. This implementation
    // handles just GET /tournament/<guid>.wrt with Connection: close, one
    // request per accepted connection.
    public sealed class TournamentHttpServer : ITournamentHttpServer
    {
        private const string UrlPrefix = "/tournament/";
        private const string UrlSuffix = ".wrt";
        private const int MaxHeaderLine = 8192;
        private const int ReceiveTimeoutMs = 5000;
        private const int SendTimeoutMs = 10000;

        private readonly object _stateLock = new object();
        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _running;
        private Guid _servedId;
        private string _servedPath;

        public event EventHandler<string> DiagnosticMessage;
        public int? ActualPort { get; private set; }

        public void SetServedTournament(Guid tournamentId, string wrtPath)
        {
            lock (_stateLock)
            {
                _servedId = tournamentId;
                _servedPath = wrtPath;
            }
        }

        public void Start(int port)
        {
            Stop();

            TcpListener listener;
            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                try { listener.Start(); }
                catch (SocketException ex) when (port != 0 &&
                    (ex.SocketErrorCode == SocketError.AddressAlreadyInUse || ex.SocketErrorCode == SocketError.AccessDenied))
                {
                    listener.Stop();
                    listener = new TcpListener(IPAddress.Any, 0);
                    listener.Start();
                }
            }
            catch (Exception ex)
            {
                DiagnosticMessage?.Invoke(this, string.Format(ProviderLocalization.T("Network_HttpStartFailed", "Не удалось запустить передачу данных через HTTP: {0}"), ex.Message));
                return;
            }

            lock (_stateLock)
            {
                _listener = listener;
                ActualPort = ((IPEndPoint)listener.LocalEndpoint).Port;
                _running = true;
                _acceptThread = new Thread(() => AcceptLoop(listener)) { IsBackground = true, Name = "TournamentHttp.Accept" };
                _acceptThread.Start();
            }
        }

        public void Stop()
        {
            lock (_stateLock)
            {
                _running = false;
                try { _listener?.Stop(); } catch { }
                _listener = null;
                ActualPort = null;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        private void AcceptLoop(TcpListener listener)
        {
            while (_running && listener != null)
            {
                TcpClient client;
                try
                {
                    client = listener.AcceptTcpClient();
                }
                catch (SocketException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (InvalidOperationException)
                {
                    break;
                }
                catch
                {
                    continue;
                }

                ThreadPool.QueueUserWorkItem(_ => HandleClient(client));
            }
        }

        private void HandleClient(TcpClient client)
        {
            try
            {
                client.ReceiveTimeout = ReceiveTimeoutMs;
                client.SendTimeout = SendTimeoutMs;
                using (var stream = client.GetStream())
                {
                    var requestLine = ReadLine(stream);
                    if (string.IsNullOrEmpty(requestLine)) return;

                    // Parse headers, looking for Content-Length so we can drain
                    // the request body before responding. If we close the
                    // connection with unread data in the receive buffer,
                    // Windows sends an abortive RST — the client then sees a
                    // "connection reset" instead of our well-formed response.
                    int contentLength = 0;
                    string header;
                    do
                    {
                        header = ReadLine(stream);
                        if (!string.IsNullOrEmpty(header) &&
                            header.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                        {
                            int.TryParse(header.Substring("Content-Length:".Length).Trim(), out contentLength);
                        }
                    } while (!string.IsNullOrEmpty(header));

                    if (contentLength > 0)
                    {
                        var drainBuf = new byte[Math.Min(contentLength, 8192)];
                        int remaining = contentLength;
                        while (remaining > 0)
                        {
                            int read;
                            try { read = stream.Read(drainBuf, 0, Math.Min(drainBuf.Length, remaining)); }
                            catch { break; }
                            if (read <= 0) break;
                            remaining -= read;
                        }
                    }

                    var parts = requestLine.Split(' ');
                    if (parts.Length < 2 || parts[0] != "GET")
                    {
                        WriteStatus(stream, 405, "Method Not Allowed");
                        return;
                    }

                    var path = parts[1];
                    Guid requested;
                    if (!TryParseTournamentPath(path, out requested))
                    {
                        WriteStatus(stream, 404, "Not Found");
                        return;
                    }

                    Guid servedId;
                    string servedPath;
                    lock (_stateLock)
                    {
                        servedId = _servedId;
                        servedPath = _servedPath;
                    }

                    if (servedId == Guid.Empty || requested != servedId)
                    {
                        WriteStatus(stream, 404, "Not Found");
                        return;
                    }
                    if (string.IsNullOrEmpty(servedPath))
                    {
                        WriteStatus(stream, 404, "Not Found");
                        return;
                    }

                    byte[] fileBytes;
                    try
                    {
                        fileBytes = ReadSnapshot(servedPath);
                    }
                    catch (FileNotFoundException)
                    {
                        WriteStatus(stream, 404, "Not Found");
                        return;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        WriteStatus(stream, 404, "Not Found");
                        return;
                    }
                    catch (Exception ex)
                    {
                        DiagnosticMessage?.Invoke(this, ProviderLocalization.T("Network_WrtReadError", "Ошибка чтения .wrt для раздачи: ") + ex.Message);
                        WriteStatus(stream, 500, "Internal Server Error");
                        return;
                    }

                    var headerBytes = Encoding.ASCII.GetBytes(
                        "HTTP/1.1 200 OK\r\n" +
                        "Content-Type: application/json; charset=utf-8\r\n" +
                        "Content-Length: " + fileBytes.Length + "\r\n" +
                        "Connection: close\r\n" +
                        "\r\n");
                    stream.Write(headerBytes, 0, headerBytes.Length);
                    stream.Write(fileBytes, 0, fileBytes.Length);
                    stream.Flush();
                }
            }
            catch
            {
                // Client disconnect, SSL probe, or similar — ignore.
            }
            finally
            {
                try { client.Close(); } catch { }
            }
        }

        private static byte[] ReadSnapshot(string path)
        {
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    // Keep reading the opened snapshot when an atomic save
                    // replaces the path. Windows can still briefly reject an
                    // open during ReplaceFile itself (sharing error or a briefly
                    // missing path); retry before reporting a failed download.
                    using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    using var buffer = new MemoryStream();
                    file.CopyTo(buffer);
                    return buffer.ToArray();
                }
                catch (IOException ex) when (attempt < 5 &&
                    (ex is FileNotFoundException || (ex.HResult & 0xffff) == 32 || (ex.HResult & 0xffff) == 33))
                {
                    Thread.Sleep(10 * (attempt + 1));
                }
            }
        }

        private static bool TryParseTournamentPath(string path, out Guid id)
        {
            id = Guid.Empty;
            if (path == null) return false;
            if (!path.StartsWith(UrlPrefix, StringComparison.Ordinal)) return false;
            if (!path.EndsWith(UrlSuffix, StringComparison.Ordinal)) return false;
            var idPart = path.Substring(UrlPrefix.Length, path.Length - UrlPrefix.Length - UrlSuffix.Length);
            return Guid.TryParse(idPart, out id);
        }

        private static string ReadLine(NetworkStream stream)
        {
            var sb = new StringBuilder();
            int b;
            while ((b = stream.ReadByte()) != -1)
            {
                if (b == '\r') continue;
                if (b == '\n') return sb.ToString();
                sb.Append((char)b);
                if (sb.Length > MaxHeaderLine) return null;
            }
            return sb.Length == 0 ? null : sb.ToString();
        }

        private static void WriteStatus(NetworkStream stream, int code, string phrase)
        {
            var body = Encoding.UTF8.GetBytes(phrase);
            var header = Encoding.ASCII.GetBytes(
                "HTTP/1.1 " + code + " " + phrase + "\r\n" +
                "Content-Type: text/plain; charset=utf-8\r\n" +
                "Content-Length: " + body.Length + "\r\n" +
                "Connection: close\r\n" +
                "\r\n");
            stream.Write(header, 0, header.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }
    }
}
