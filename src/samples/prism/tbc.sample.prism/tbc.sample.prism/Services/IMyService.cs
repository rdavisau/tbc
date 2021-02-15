using System.Threading.Tasks;

namespace tbc.sample.prism
{
    public interface IMyService
    {
        Task<string> GetAString();
    }
}