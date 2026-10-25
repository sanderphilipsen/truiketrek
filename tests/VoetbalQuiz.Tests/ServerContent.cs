using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace VoetbalQuiz.Tests;

/// <summary>De echte inhoud van de server (Data/players, Data/questions), voor tests die de seeders gebruiken.</summary>
public static class ServerContent
{
    /// <summary>De map src/VoetbalQuiz.Server, gevonden vanaf de testuitvoer.</summary>
    public static string Root { get; } = FindRoot();

    public static IWebHostEnvironment Environment() => new Env(Root);

    /// <summary>Een andere map met een eigen Data/questions, voor tests met enkel hun eigen inhoud.</summary>
    public static IWebHostEnvironment Environment(string root) => new Env(root);

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "VoetbalQuiz.slnx"))) dir = dir.Parent;
        return Path.Combine(dir!.FullName, "src", "VoetbalQuiz.Server");
    }

    private sealed class Env(string root) : IWebHostEnvironment
    {
        public string ContentRootPath { get; set; } = root;
        public string WebRootPath { get; set; } = root;
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "VoetbalQuiz.Server";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
