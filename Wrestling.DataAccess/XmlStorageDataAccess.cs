using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Wrestling.DataAccess
{
    public class XmlStorageDataAccess : IStorageDataAccess
    {
        public void ProcessDirectory<T>(string targetDirectory, ref List<T> list, string mask)
        {
            // Process the list of files found in the directory. 
            string[] fileEntries = Directory.GetFiles(targetDirectory, string.IsNullOrEmpty(mask) ? "*.xml" : mask);
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

        public T ReadFromFile<T>(string path)
        {
            var fullPath = path.Contains(".xml") ? path : path + ".xml";

            var content = GetXmlString(fullPath);
            return Deserialize<T>(content);
        }

        public bool SaveToFile<T>(T item, string fileName)
        {
            var serializerObj = new XmlSerializer(typeof(T));

            var fullPath = fileName.Contains(".xml") ? fileName : fileName + ".xml";

            try
            {
                using (var writeFileStream = new StreamWriter(fullPath))
                {
                    serializerObj.Serialize(writeFileStream, item);
                }
            }
            catch
            {
                return false;
            }
            return true;
        }

        public Task<bool> SaveToFileAsync<T>(T item, string fileName)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> GetFileNamesInDirectory(string path, string mask)
        {
            return Directory.GetFiles(path, string.IsNullOrEmpty(mask) ? "*.xml" : mask);
        }

        public void SaveToStorage<T>(T item, string storageFolder, string fileName)
        {
            SaveToFile(item, storageFolder + fileName + ".xml");
        }

        #region Common methods

        private static T Deserialize<T>(string xmlContent)
        {
            T result;

            var serializer = new XmlSerializer(typeof(T));

            using (var reader = new StringReader(xmlContent))
            {
                result = (T)serializer.Deserialize(reader);
            }

            return result;
        }

        private static string GetXmlString(string strFile)
        {
            string result;

            var xmlDoc = LoadXmlDocument(strFile);

            using (var writer = new StringWriter())
            {
                using (var xmlWriter = new XmlTextWriter(writer))
                {
                    xmlDoc.WriteTo(xmlWriter);
                    result = writer.ToString();
                }
            }

            return result;
        }

        private static async Task<string> GetXmlStringAsync(string strFile)
        {
            string result;

            var xmlDoc = await LoadXmlDocumentAsync(strFile);

            using (var writer = new StringWriter())
            {
                using (var xmlWriter = new XmlTextWriter(writer))
                {
                    xmlDoc.WriteTo(xmlWriter);
                    result = writer.ToString();
                }
            }

            return result;
        }

        private static XmlDocument LoadXmlDocument(string filePath)
        {
            var xmlDoc = new XmlDocument();

            try
            {
                xmlDoc.Load(filePath);
            }
            catch (XmlException e)
            {
                Console.WriteLine(e.Message);
            }

            return xmlDoc;
        }

        private static async Task<XmlDocument> LoadXmlDocumentAsync(string filePath)
        {
            var xmlDoc = new XmlDocument();

            try
            {
                using (var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 4096,
                    useAsync: true))
                {
                    var settings = new XmlReaderSettings
                    {
                        Async = true,
                        IgnoreWhitespace = true,
                        IgnoreComments = true
                    };

                    using (var reader = XmlReader.Create(stream, settings))
                    {
                        // This will load the document asynchronously
                        await Task.Run(() => xmlDoc.Load(reader)).ConfigureAwait(false);
                    }
                }
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
            catch (XmlException e)
            {
                Console.WriteLine(e.Message);
                return null;
            }
            catch (IOException e)
            {
                Console.WriteLine(e.Message);
                return null;
            }

            return xmlDoc;
        }

        public async Task<T> ReadFromFileAsync<T>(string path)
        {
            var fullPath = path.Contains(".xml") ? path : path + ".xml";

            var content = await GetXmlStringAsync(fullPath);
            return Deserialize<T>(content);
        }

        #endregion
    }
}
