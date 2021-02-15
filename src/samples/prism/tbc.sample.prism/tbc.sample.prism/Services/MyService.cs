using System;
using System.Threading.Tasks;

namespace tbc.sample.prism
{
    public class MyService : ServiceBase, IMyService
    {
        public async Task<string> GetAString()
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            
            return "the string";
        }
    }
}