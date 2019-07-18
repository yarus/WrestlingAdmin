using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Wrestling.DataAccess
{
    public class JsonStorageDataAccess : IStorageDataAccess
    {
        public void ProcessDirectory<T>(string targetDirectory, ref List<T> list, string mask)
        {
            // Process the list of files found in the directory. 
            string[] fileEntries = Directory.GetFiles(targetDirectory, string.IsNullOrEmpty(mask) ? "*.*" : mask);

            foreach (string fileName in fileEntries)
            {
                list.Add(this.ReadFromFile<T>(fileName));
            }

            // Recurse into subdirectories of this directory. 
            string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);

            foreach (string subdirectory in subdirectoryEntries)
            {
                this.ProcessDirectory(subdirectory, ref list, mask);
            }
        }

        public void SaveToStorage<T>(T item, string storageFolder, string fileName)
        {
            SaveToFile(item, storageFolder + fileName);
        }

        public async Task<bool> SaveToFileAsync<T>(T item, string fileName)
        {
            try
            {
                using (var file = File.Open(fileName, FileMode.Create))
                {
                    using (var stream = new MemoryStream())
                    {
                        using (var writer = new StreamWriter(stream))
                        {
                            var serizlizer = JsonSerializer.CreateDefault();
                            serizlizer.Serialize(writer, item);

                            await writer.FlushAsync().ConfigureAwait(false);

                            stream.Seek(0, SeekOrigin.Begin);

                            await stream.CopyToAsync(file).ConfigureAwait(false);
                        }
                    }

                    await file.FlushAsync().ConfigureAwait(false);
                }

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool SaveToFile<T>(T item, string fileName)
        {
            try
            {
                using (var writeFileStream = new StreamWriter(fileName))
                {
                    var jsonWriter = new JsonTextWriter(writeFileStream);
                    var ser = new JsonSerializer();
                    ser.Serialize(jsonWriter, item);
                    jsonWriter.Flush();
                }
            }
            catch(Exception ex)
            {
                return false;
            }
            return true;
        }

        public T ReadFromFile<T>(string path)
        {
            T result;

            if (!File.Exists(path))
            {
                return default(T);
            }

            try
            {
                using (var reader = new StreamReader(path))
                {
                    var jsonReader = new JsonTextReader(reader);
                    var ser = new JsonSerializer();
                    result = ser.Deserialize<T>(jsonReader);
                }
            }
            catch
            {
                return default(T);
            }

            return result;
        }

        public IEnumerable<string> GetFileNamesInDirectory(string path, string mask)
        {
            return Directory.GetFiles(path, string.IsNullOrEmpty(mask) ? "*.*" : mask);
        }
    }
}
