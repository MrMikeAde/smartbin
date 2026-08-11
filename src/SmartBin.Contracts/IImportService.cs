using System.Threading;
using System.Threading.Tasks;

namespace SmartBin.Contracts
{
    public interface IImportService
    {
        /// <summary>
        /// Imports a user-selected file into the controlled SmartBin storage area, computing hashes and recording metadata.
        /// Does not delete the original file.
        /// </summary>
        Task<ISmartBinItem> ImportFileAsync(string sourcePath, CancellationToken cancellationToken = default);
    }
}
