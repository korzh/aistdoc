using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;

using Microsoft.Extensions.Configuration;

using McMaster.Extensions.CommandLineUtils;

using LibGit2Sharp;

namespace aistdoc
{
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
        public string PrevVersion { get; set; } = "";
        public string NewVersion { get; set; } = "";
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

    class ReleaseNotesCommand : ICommand
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

            Func<int> runCommandFunc = new ReleaseNotesCommand(projectArg, configOp, patOp, outputOp).Run;
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

        string ConfigPath => _configOp.HasValue()
                                        ? _configOp.Value()
                                        : "aistdoc.json";
        string PAT => _patOp.Value();

        string OutputPath => _outputOp.HasValue()
                                        ? _outputOp.Value()
                                        : "Release notes.md";
        string ProjectId => _projectArg.Value;

        protected ReleaseNotesCommand(
            CommandArgument<string> projectArg, 
            CommandOption<string> configOp, 
            CommandOption<string> patOp, 
            CommandOption<string> outputOp)
        {
            _projectArg = projectArg;
            _configOp = configOp;
            _patOp = patOp;
            _outputOp = outputOp;
        }

        public int Run()
        {
            try {
                ReadSettingsFromConfigFile();
                var project = GetProject();

                var commitGroups = GetCommitGroups(project);

                var releaseNotes = BuildReleaseNotes(project, commitGroups);

                File.WriteAllText(OutputPath, releaseNotes);

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

        private void ReadSettingsFromConfigFile()
        {
            var builder = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory());
            Console.WriteLine($"Reading config: {ConfigPath} ...");
            try {
                builder.AddJsonFile(ConfigPath);
                var config = builder.Build();
                _gitSettings = config.GetSection("git").Get<GitSettings>();
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

        private void WriteCommitWithType(MarkdownBuilder mb, CommitWithType commit) {
            var message = commit.Source.Message.Replace($"[{commit.Type}] ", "", 
                StringComparison.InvariantCultureIgnoreCase);
            var separator = message.IndexOf("\n");
            var title = message;
            string description = null;
            if (separator >= 0) {
                title = message.Substring(0, separator);
                description = message.Substring(separator + 1);
            }
            mb.List(MarkdownBuilder.MarkdownBold(title), description);
        }

        private string BuildReleaseNotes(ProjectSettings project, Dictionary<string, List<CommitWithType>> commitGroups)
        {

            var mb = new MarkdownBuilder();
            mb.Header(1, "Release notes " + project.NewVersion);
            mb.AppendLine();

            if (commitGroups.TryGetValue("NEW", out var newCommits)) {
                mb.Header(3, "Added");
                mb.AppendLine();

                foreach (var commit in newCommits) {
                    WriteCommitWithType(mb, commit);
                }
            }

            if (commitGroups.TryGetValue("UPD", out var updateCommits)) {
                mb.Header(3, "Updated"); 
                mb.AppendLine();

                foreach (var commit in updateCommits) {
                    WriteCommitWithType(mb, commit);
                }
            }


            if (commitGroups.TryGetValue("FIX", out var fixCommits)) {
                mb.Header(3, "Fixed");
                mb.AppendLine();

                foreach (var commit in fixCommits) {
                    WriteCommitWithType(mb, commit);
                }
            }

            return  mb.ToString();
        }
    }
}
