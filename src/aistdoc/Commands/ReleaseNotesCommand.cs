using System;
using System.Linq;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

using McMaster.Extensions.CommandLineUtils;

using LibGit2Sharp;


namespace aistdoc
{
    class ReleaseNotesCommand : ICommand
    {

        public static void Configure(CommandLineApplication command)
        {
            command.Description = "Creates release notes for new version tag from repository";
            command.HelpOption("-?|-h|--help");

            var fileNameOp = command.Option("--conifg:<filename> | -c:<filename>", "Config file name", optionType: CommandOptionType.SingleOrNoValue);
            var modeOp = command.Option("--mode:<mode> | -m: <mode>", "The mode. cs ot ts", optionType: CommandOptionType.SingleOrNoValue)
                                .Accepts(b => b
                                    .Values("cs", "ts")
                                );

            Func<int> runCommandFunc = new ReleaseNotesCommand().Run;
            command.OnExecute(runCommandFunc);

        }

        private static Regex commitTypeRegex = new Regex(@"^\[(FIX|NEW|UPD|DOC)\]", RegexOptions.IgnoreCase);

        class CommitWithType 
        { 
            public Commit Source { get; set; }
            public string Type { get; set; }
        }

        public int Run()
        {
            string repoPath = "";
            if (!Repository.IsValid(repoPath)) {
                return -1;
            }

            using (var repo = new Repository(repoPath))
            {

                var prevVersion = "5.3.4";
                var nextVersion = "5.3.5";

                Tag tagFrom = repo.Tags["v" + prevVersion];
                Tag tagTo   = repo.Tags["v" + nextVersion];

                var commitGroups = repo.Commits
                    .QueryBy(new CommitFilter
                    {
                        ExcludeReachableFrom = tagFrom,
                        IncludeReachableFrom = tagTo ?? (object)repo.Head
                    })
                    .Select(c => {
                        var match = commitTypeRegex.Match(c.MessageShort ?? "");
                        return new CommitWithType
                        {
                            Source = c,
                            Type = match.Success
                                ? match.Groups[1].ToString().ToUpperInvariant()
                                : null
                        };
                    })
                    .Where(cwt => cwt.Type != null)
                    .GroupBy(cwt => cwt.Type)
                    .ToDictionary(g => g.Key, g => g.ToList());

                var mb = new MarkdownBuilder();
                mb.Header(1, "Release notes " + nextVersion);

                if (commitGroups.TryGetValue("NEW", out var newCommits)) {
                    mb.Header(3, "Added");
                    foreach (var commit in newCommits) {
                        WriteCommitWithType(mb, commit);
                    }
                }

                if (commitGroups.TryGetValue("UPD", out var updateCommits)) {
                    mb.Header(3, "Updated");
                    foreach (var commit in updateCommits) {
                        WriteCommitWithType(mb, commit);
                    }
                }


                if (commitGroups.TryGetValue("FIX", out var fixCommits)) {
                    mb.Header(3, "Fixed");
                    foreach (var commit in fixCommits) {
                        WriteCommitWithType(mb, commit);
                    }
                }

                File.WriteAllText("test.md", mb.ToString());
            }

            return 0;
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
            mb.List(MarkdownBuilder.MarkdownItalic(title), description);
        }
    }
}
