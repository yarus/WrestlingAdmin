using System;

namespace Wrestling.Providers.Network
{
    public interface ITournamentHttpServer : IDisposable
    {
        event EventHandler<string> DiagnosticMessage;

        // null when the server is not running (e.g. port conflict).
        int? ActualPort { get; }

        void SetServedTournament(Guid tournamentId, string wrtPath);

        void Start(int port);

        void Stop();
    }
}
