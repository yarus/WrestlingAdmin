using Wrestling.DataAccess;
using Wrestling.Recorder;
using Wrestling.Recorder.DataAccess;

namespace Wrestling.UI.Material.Utils.Recording
{
    public class RecorderConfigurationDataAccess : IRecorderConfigurationDataAccess
    {
        private readonly IStorageDataAccess _storageDataAccess;

        public RecorderConfigurationDataAccess(IStorageDataAccess storageDataAccess)
        {
            _storageDataAccess = storageDataAccess;
        }

        public bool SaveToFile(RecorderConfiguration item, string fileName)
        {
            return _storageDataAccess.SaveToFile(item, fileName);
        }

        public RecorderConfiguration LoadFromFile(string fileName)
        {
            return _storageDataAccess.ReadFromFile<RecorderConfiguration>(fileName);
        }
    }
}
