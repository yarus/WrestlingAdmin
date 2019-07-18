namespace Wrestling.UI.Utils
{
    public interface IDiContainer
    {
        T Resolve<T>() where T : class;
        object Resolve(string key);
        void Add<T>(object item) where T : class;
        void Add(object item, string key);
        void Remove<T>() where T : class;
        void Remove(string key);
    }
}