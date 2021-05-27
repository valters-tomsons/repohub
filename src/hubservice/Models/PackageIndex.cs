namespace hubservice.Models
{
    public class PackageIndex
    {
        public Aur Aur { get; set; }
        public Git Git { get; set; }
    }

    public class Aur
    {
        public string[] x86_64 { get; set; }
        public string[] aarch64 { get; set; }
    }

    public class Git
    {
        public string[] x86_64 { get; set; }
        public string[] aarch64 { get; set; }
    }
}