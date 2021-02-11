namespace Tbc.Host.Services.Patch.Models
{
    public class DisambiguatedCSharpClassSource : CSharpClassSource
    {
        public string ClassName { get; set; }
        public string Namespace { get; set; }
    }
}