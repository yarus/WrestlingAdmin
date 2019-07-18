namespace Wrestling.Recorder.DataAccess
{
    public interface IRecorderConfigurationDataAccess
    {
        RecorderConfiguration LoadFromFile(string fileName);
        bool SaveToFile(RecorderConfiguration item, string fileName);
    }
}