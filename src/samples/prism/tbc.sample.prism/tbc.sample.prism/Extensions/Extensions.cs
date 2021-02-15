using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Prism.Services;

namespace tbc.sample.prism.Extensions
{
    public static class Extensions
    {
        private const string CancelTitle = "Cancel";
        
        public static async Task<T> PresentChoice<T>(
            this IPageDialogService pageDialogService, string title, 
            ICollection<T> choices, Func<T, string> titleSelector, object anchor = null)
        {
            var choiceDict = choices.ToDictionary(titleSelector, c => c);

            var tcs = new TaskCompletionSource<T>();
            var buttons = choiceDict.Select(x => ActionSheetButton.CreateButton(x.Key, () => tcs.TrySetResult(x.Value))).ToList();
            var cancel = ActionSheetButton.CreateButton(CancelTitle, () => tcs.TrySetResult(default(T)));

            await pageDialogService.DisplayActionSheetAsync(title, buttons.Concat(new[] {cancel}).ToArray());

            return await tcs.Task;
        }
    }
}