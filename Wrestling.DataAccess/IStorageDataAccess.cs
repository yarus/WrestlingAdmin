using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wrestling.DataAccess
{
    public interface IStorageDataAccess
    {
        void ProcessDirectory<T>(string targetDirectory, ref List<T> list, string mask);

        void SaveToStorage<T>(T item, string storageFolder, string fileName);

        T ReadFromFile<T>(string path);
        Task<T> ReadFromFileAsync<T>(string path, CancellationToken cancellationToken = default);
        bool SaveToFile<T>(T item, string fileName);
        Task<bool> SaveToFileAsync<T>(T item, string fileName, CancellationToken cancellationToken = default);
        IEnumerable<string> GetFileNamesInDirectory(string path, string mask);
    }
}
