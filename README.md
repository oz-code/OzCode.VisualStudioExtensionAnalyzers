# OzCode.VisualStudioExtensionAnalyzers
A collection of Roslyn Analyzers that help in creating Visual Studio extensions.


## Add try..catch in to report exceptions to telemetry in critical paths
This analyzer is meant to help Visual Studio extension authors make sure important exceptions are caught and properly reported to telemetry (i.e. an exception monitoring tool such as Raygun, Exceptionless, Application Insights, etc).
The analyzer adds a try..catch block and and a call Logger.LogException() in methods and constructors where there will dire consequences if an exception is thrown within.
This includes:
1) MEF ImportingConstructors, where an unhandled exception may prevent your extension from being properly MEF-composed and initialized.
2) Callbacks which implement Visual Studio SDK interfaces (e.g. IWpfTextViewCreationListener, or any interface defined under Microsoft.VisualStudio.X namespace), where an unhandled exception may pop up an annoying and cryptic MessageBox, which says "An exception has been encountered. This may be caused by an exception."
![Screenshot](VisualStudioTryCatchAnalyzer.png?raw=true "Title")
