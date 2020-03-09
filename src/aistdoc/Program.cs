﻿using System;
using System.IO;

using McMaster.Extensions.CommandLineUtils;

namespace aistdoc
{

    class Program 
    {

        static int Main(string[] args) 
        {

            System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
            Console.WriteLine($"aistdoc utility {assembly.GetName().Version.ToString()} (c) Aistant 2018-2019");
            Console.WriteLine("Current folder: " + Directory.GetCurrentDirectory());

            var app = new CommandLineApplication();
            RootCommand.Configure(app);
            return app.Execute(args);
        }

    }

    // Commands/RootCommand.cs
    public class RootCommand : ICommand
    {
        public static void Configure(CommandLineApplication app)
        {
            app.Name = "aistdoc";
            app.HelpOption("-?|-h|--help");

            // Register commands
            app.Command("publish", c => PublsihDocCommand.Configure(c));
            app.Command("create", c => CreateConfigCommand.Configure(c));
            app.Command("release-notes", c => ReleaseNotesCommand.Configure(c));

            Func<int> runCommandFunc = new RootCommand(app).Run;
            app.OnExecute(runCommandFunc);
        }

        private readonly CommandLineApplication _app;

        public RootCommand(CommandLineApplication app)
        {
            _app = app;
        }

        public int Run()
        {
            _app.ShowHelp();

            return 0;
        }
    }

    class UnknownParameterException : Exception 
    {
        public UnknownParameterException(string message):base(message) 
        {

        }
    }
}
