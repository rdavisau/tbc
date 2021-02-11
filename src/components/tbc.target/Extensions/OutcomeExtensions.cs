using System.Linq;
using Tbc.Protocol;

namespace Tbc.Target.Extensions
{
    public class TbcOutcome
    {
        public static Outcome Success() =>
            new Outcome {Success = true};

        public static Outcome Failure(params string[] why) =>
            new Outcome
            {
                Success = false,
                Messages = {why.Select(x => new Message {Message_ = x})}
            };
    }
}