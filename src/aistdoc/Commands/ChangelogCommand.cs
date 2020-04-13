using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

using Microsoft.Extensions.Configuration;

using McMaster.Extensions.CommandLineUtils;

using LibGit2Sharp;
using Aistant.KbService;

namespace aistdoc
{ 
    class Version {

        private static readonly Regex _versionRegex = new Regex(@"(\d+)?\.(\d+?)\.(\d+)(-(alpha|beta|rc)(\d+))?");
        public int Major { get; private set; } = 0;
        public int Minor { get; private set; } = 0;
        public int Patch { get; private set; } = 0;
        public string PreRelease { get; private set; }

        public bool IsRelease() => PreRelease == null;

        public override string ToString()
        {
            var mainVersion = $"{Major}.{Minor}.{Patch}";
            if (IsRelease()) {
                return mainVersion;
            }

            return $"{mainVersion}-{PreRelease}";
        }

        public string GetVersionWithourPreRelease()
        {
            return $"{Major}.{Minor}.{Patch}";
        }

        public Version(string version)
        {
            var match = _versionRegex.Match(version);
            if (!match.Success)
                throw new FormatException("Invalid representation of version. Expected: " + _versionRegex.ToString());

            Major = int.Parse(match.Groups[1].Value);
            Minor = int.Parse(match.Groups[2].Value);
            Patch = int.Parse(match.Groups[3].Value);

            if (match.Groups.Count > 4) {
                PreRelease = match.Groups[5].Value + match.Groups[6].Value;
            }
        }
    }


    class CredentialSettings
    {
        public string Id { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string AccessToken { get; set; }
        public bool Default { get; set; } = false;
    }

    class ProjectSettings
    { 
        public string Id { get; set; }
        public string TitleTemplate { get; set; } = "Version ${VersionNum}";
        public string LogItemTemplate { get; set; } = "__[${ItemType}]__: ${ItemTitle}    ${ItemDescription}\n";
        public string DateItemTemplate { get; set; } = "<div class=\"aist-article-updated\"><span>${ReleasedDate}</span></div>\n";
        public string PrevVersion { get; set; } = "";
        public string NewVersion { get; set; } = "";
        public string Changelog { get; set; }
        public List<RepositorySettings> Repositories { get; set; } = new List<RepositorySettings>();
    }
    
    class RepositorySettings
    { 
        public string CredentialId { get; set; }
        public string Path { get; set; }
        public string Url { get; set; }
        public string Branch { get; set; } = "master";
        public bool CloneIfNotExist { get; set; } = false;
    }

    class GitSettings  
    {
        public List<CredentialSettings> Credentials { get; set; } = new List<CredentialSettings>();
        public List<ProjectSettings> Projects { get; set; } = new List<ProjectSettings>();
    }

    class ChangelogCommand : ICommand
    {

        public static void Configure(CommandLineApplication command)
        {
            command.Description = "Creates release notes for new version tag from repository";
            command.HelpOption("-?|-h|--help");

            var projectArg = command.Argument<string>("project", "The project id")
                                    .IsRequired();
            var configOp = command.Option<string>("--config:<filename> | -c:<filename>", "Config file name", optionType: CommandOptionType.SingleOrNoValue);
            var patOp = command.Option<string>("--pat:<token>", "The personal access token", optionType: CommandOptionType.SingleOrNoValue);
            var outputOp = command.Option<string>("--output:<filename> | -o:<filename>", "Output file", optionType: CommandOptionType.SingleOrNoValue);
            var versionOp = command.Option<string>("--version:<version> | -v:<version>", "The version", optionType: CommandOptionType.SingleOrNoValue)
                ;
            Func<int> runCommandFunc = new ChangelogCommand(projectArg, configOp, patOp, outputOp, versionOp).Run;
            command.OnExecute(runCommandFunc);

        }

        private static Regex commitTypeRegex = new Regex(@"^\[(FIX|NEW|UPD|DOC)\]", RegexOptions.IgnoreCase);
        private static Regex tagVersionRegex = new Regex(@"v(\d+)?\.(\d+?)\.(\d+)?");
        class CommitWithType 
        { 
            public Commit Source { get; set; }
            public string Type { get; set; }
        }

        CommandArgument<string> _projectArg;
        CommandOption<string> _configOp;
        CommandOption<string> _patOp;
        CommandOption<string> _outputOp;
        CommandOption<string> _versionOp;

        string ConfigPath => _configOp.HasValue()
                                        ? _configOp.Value()
                                        : "aistdoc.json";
        string PAT => _patOp.Value();

        string OutputPath => _outputOp.HasValue()
                                        ? _outputOp.Value()
                                        : "Release notes.md";

        string Version => _versionOp.Value();

        string ProjectId => _projectArg.Value;

        protected ChangelogCommand(
            CommandArgument<string> projectArg, 
            CommandOption<string> configOp, 
            CommandOption<string> patOp, 
            CommandOption<string> outputOp,
            CommandOption<string> versionOp)
        {
            _projectArg = projectArg;
            _configOp = configOp;
            _patOp = patOp;
            _outputOp = outputOp;
            _versionOp = versionOp;
        }

        public int Run()
        {
            try {
                ReadSettingsFromConfigFile();
                var project = GetProject();

                var commitGroups = GetCommitGroups(project);

                var releaseNotes = BuildReleaseNotes(project, commitGroups);
                File.WriteAllText(OutputPath, releaseNotes);

                var changelog = _aistantSettings.Changelogs.Find(cl => cl.Id == project.Changelog);
                if (!string.IsNullOrEmpty(changelog?.Uri)) {
                    PublishToAistant(project, changelog, releaseNotes);
                }

                return 0;
            }
            catch (Exception ex) {
                var currenColor = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
                Console.ForegroundColor = currenColor;
                return -1;
            }
        }

        private GitSettings _gitSettings;
        private AistantSettings _aistantSettings;

        private void ReadSettingsFromConfigFile()
        {
            var builder = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory());
            Console.WriteLine($"Reading config: {ConfigPath} ...");
            try {
                builder.AddJsonFile(ConfigPath);
                var config = builder.Build();
                _gitSettings = config.GetSection("git").Get<GitSettings>();
                _aistantSettings = config.GetSection("aistant").Get<AistantSettings>();
            }
            catch (FileNotFoundException ex) {
                throw ex;
            }
        }

        private ProjectSettings GetProject()
        {
            var project = _gitSettings.Projects.FirstOrDefault(p => p.Id == ProjectId);
            if (project == null)
                throw new Exception($"Project with Id '{ProjectId}' hasn't been found");
            return project;
        }

        private Dictionary<string, List<CommitWithType>> GetCommitGroups(ProjectSettings projectSettings)
        {
            return projectSettings.Repositories
                .SelectMany(rs => {
                    var repo = GetRepository(rs);

                    var prevVersion = projectSettings.PrevVersion;
                    var nextVersion = projectSettings.NewVersion;

                    Tag tagFrom = repo.Tags["v" + prevVersion];
                    if (tagFrom == null) {
                        tagFrom = repo.Tags.OrderByDescending(t => t.Annotation.Tagger.When)
                                           .FirstOrDefault(t => tagVersionRegex.IsMatch(t.FriendlyName));
                    }

                    Tag tagTo = repo.Tags["v" + nextVersion];

                    return repo.Commits
                        .QueryBy(new CommitFilter
                        {
                            ExcludeReachableFrom = tagFrom,
                            IncludeReachableFrom = tagTo ?? (object)repo.Head
                        })
                        .Select(c =>
                        {
                            var match = commitTypeRegex.Match(c.MessageShort ?? "");
                            return new CommitWithType
                            {
                                Source = c,
                                Type = match.Success
                                    ? match.Groups[1].ToString().ToUpperInvariant()
                                    : null
                            };
                        })
                        .Where(cwt => cwt.Type != null);
                })
                .GroupBy(cwt => cwt.Type)
                .ToDictionary(g => g.Key, g => g.ToList());
        }


        private Repository GetRepository(RepositorySettings repoSettings) 
        {
            CloneRepositoryIfNotExist(repoSettings);
            var repo = new Repository(repoSettings.Path);

            var branch = repo.Branches[repoSettings.Branch];
            if (branch == null)
                throw new Exception("Branch is not found " + repoSettings.Branch);

            branch = Commands.Checkout(repo, branch);
            if (branch.TrackingDetails.BehindBy.HasValue && branch.TrackingDetails.BehindBy >= 0) {

                LibGit2Sharp.PullOptions options = new LibGit2Sharp.PullOptions() {
                    FetchOptions = new FetchOptions()
                    {
                        CredentialsProvider = (url, usernameFromUrl, types) =>
                           GetCredentials(repoSettings.CredentialId)
                    },
                    MergeOptions = new MergeOptions()
                    {
                        FailOnConflict = true
                    }     
                };
                
                // User information to create a merge commit
                var signature = new LibGit2Sharp.Signature(
                    new Identity("Mock", "Mock"), DateTimeOffset.Now);

                // Pull
                Commands.Pull(repo, signature, options);
            }

            return repo;
        }

        private void CloneRepositoryIfNotExist(RepositorySettings repoSettings)
        {
            if (!Repository.IsValid(repoSettings.Path)) {
                if (repoSettings.CloneIfNotExist) {
                    var co = new CloneOptions();
                    co.CredentialsProvider = (_url, _user, _cred)
                        => GetCredentials(repoSettings.CredentialId);
                    Repository.Clone(repoSettings.Url, repoSettings.Path, co);
                } 
                else {
                    throw new Exception("Local repository does not exist or invalid: " + repoSettings.Path);
                }
            };
        }

        private Credentials GetCredentials(string id)
        {
            if (!string.IsNullOrEmpty(PAT)) {
                return new UsernamePasswordCredentials { Username = PAT, Password = string.Empty };
            }

            if (string.IsNullOrEmpty(id))
                return GetDefaultCredentials();

            var credentials = _gitSettings.Credentials.FirstOrDefault(c => c.Id == id);
            if (credentials == null)
                throw new Exception($"Credentials with Id '{ProjectId}' hasn't been found");

            return !string.IsNullOrEmpty(credentials.AccessToken)
                ? new UsernamePasswordCredentials { Username = credentials.AccessToken, Password = string.Empty }
                : new UsernamePasswordCredentials { Username = credentials.UserName, Password = credentials.Password };
        }

        private Credentials GetDefaultCredentials()
        {
            var credentials = _gitSettings.Credentials.FirstOrDefault(c => c.Default);
            if (credentials == null)
                credentials = _gitSettings.Credentials.FirstOrDefault();

            if (credentials == null)
                return new DefaultCredentials();

            return !string.IsNullOrEmpty(credentials.AccessToken) 
                ? new UsernamePasswordCredentials { Username = credentials.AccessToken, Password = string.Empty}
                : new UsernamePasswordCredentials { Username = credentials.UserName, Password = credentials.Password };
        }

        private void WriteCommitWithType(MarkdownBuilder mb, ProjectSettings project,  CommitWithType commit, string prefix) {
            var message = commit.Source.Message.Replace($"[{commit.Type}] ", "", 
                StringComparison.InvariantCultureIgnoreCase);
            var separator = message.IndexOf("\n");
            var title = message;
            string description = null;
            if (separator >= 0) {
                title = message.Substring(0, separator);
                description = message.Substring(separator + 1);
            }

            mb.List(project.LogItemTemplate
                .Replace("${ItemType}", prefix)
                .Replace("${ItemTitle}", title)
                .Replace("${ItemDescription}", description));
        }

        private string BuildReleaseNotes(ProjectSettings project, Dictionary<string, List<CommitWithType>> commitGroups)
        {

            var mb = new MarkdownBuilder();
            var title = project.TitleTemplate
                .Replace("${VersionNum}", Version ?? project.NewVersion);

            mb.Header(2, title);
            mb.AppendLine();
            if (!string.IsNullOrEmpty(project.DateItemTemplate))
                mb.AppendLine(project.DateItemTemplate.Replace("${ReleasedDate}", DateTime.UtcNow.ToString("yyyy-MM-dd")));
            mb.AppendLine();

            if (commitGroups.TryGetValue("NEW", out var newCommits)) {
                foreach (var commit in newCommits) {
                    WriteCommitWithType(mb, project, commit, "New");
                }
            }

            if (commitGroups.TryGetValue("UPD", out var updateCommits)) {
                foreach (var commit in updateCommits) {
                    WriteCommitWithType(mb, project, commit, "Upd");
                }
            }

            if (commitGroups.TryGetValue("FIX", out var fixCommits)) {
                foreach (var commit in fixCommits) {
                    WriteCommitWithType(mb, project, commit, "Fix");
                }
            }

            return  mb.ToString();
        }

        private void PublishToAistant(ProjectSettings project, Changelog changelog, string releaseNotes)
        {
            var version = new Version(Version);

            var service = new AistantKbService(_aistantSettings, null);
            var article = service.GetArticleAsync(changelog.Uri, loadById: true).Result;
            if (article != null) {

                var changeLogPattern = "<div(.*?)id=\"changelog-start\"></div>";
                var divVerPattern = "<div(.*?)id=\"{0}/{1}\"(.*?)(data-released=\"(.*?)\")?(.*?)></div>";
                var changelogPatternMatch = Regex.Match(article.Content, changeLogPattern);
                if (changelogPatternMatch.Success) {

                    var divCurrVerMatch = Regex.Match(article.Content,
                        string.Format(divVerPattern, project.Id, version.GetVersionWithourPreRelease()));

                    var indexForNextVerSearch = (divCurrVerMatch.Success)
                        ? divCurrVerMatch.Index + divCurrVerMatch.Length
                        : changelogPatternMatch.Index + changelogPatternMatch.Length;

                    Match divNextVerMatch = Regex.Match(article.Content.Substring(indexForNextVerSearch),
                        string.Format(divVerPattern, "(.*?)", "(.*?)"));

                    var startIndex = divCurrVerMatch.Success
                              ? divCurrVerMatch.Index 
                              : changelogPatternMatch.Index + changelogPatternMatch.Length;

                    var endIndex = divNextVerMatch.Success
                        ? divNextVerMatch.Index + indexForNextVerSearch
                        : article.Content.Length;


                    var result = article.Content.Substring(0, startIndex);
                    result += '\n';
                    result += $"<div id=\"{project.Id}/{version.GetVersionWithourPreRelease()}\" data-released=\"{DateTime.UtcNow.ToString("yyyy-MM-dd")}\"></div>\n\n";
                    result += releaseNotes;

                    if (endIndex != article.Content.Length) {
                        result += article.Content.Substring(endIndex);
                    }

                    article.Content = result;
                }
                else {
                    var sb = new StringBuilder(article.Content)
                       .AppendLine()
                       .AppendLine("<div id=\"changelog-start\"></div>")
                       .AppendLine($"<div id=\"{project.Id}/{version.GetVersionWithourPreRelease()}\" data-released=\"{DateTime.UtcNow.ToString("yyyy-MM-dd")}\"></div>\n")
                       .Append(releaseNotes);

                    article.Content = sb.ToString();
                }
            }
            else {
                var sb = new StringBuilder()
                  .AppendLine("<div id=\"changelog-start\"></div>")
                  .AppendLine($"<div id=\"{project.Id}/{version.GetVersionWithourPreRelease()}\" data-released=\"{DateTime.UtcNow.ToString("yyyy-MM-dd")}\"></div>\n")
                  .Append(releaseNotes);


                article = new Aistant.KbService.Models.AistantArticle
                {
                    Title = "Changelog",
                    Content = sb.ToString(),
                    Excerpt = ""

                };
            }

            var successed = service.UploadArticleAsync(changelog.Uri, "Changelog", article.Content, article.Excerpt).Result;
            if (!successed) {
                throw new Exception("Article was not published");
            }

        }
    }
}
