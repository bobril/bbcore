CommandLineCommand command = Parser.Parse
(
    args: args,
    commands: new System.Collections.Generic.List<CommandLineCommand>()
    {
        new MainCommand(),
        new BuildCommand(),
        new TranslationCommand(),
        new TestCommand(),
        new BuildInteractiveCommand(),
        new BuildInteractiveNoUpdateCommand()
    }
);