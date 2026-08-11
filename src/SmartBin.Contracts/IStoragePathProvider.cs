namespace SmartBin.Contracts
{
    public interface IStoragePathProvider
    {
        /// <summary>
        /// Resolves the root directory path of the controlled SmartBin storage area.
        /// </summary>
        string GetRootPath();
    }
}
