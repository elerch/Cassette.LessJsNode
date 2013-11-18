Cassette.LessJs
===============

Version of the Less compiler for Cassette.Net using the original Js Libraries

Cassette.Less uses [dotless](https://github.com/dotless/dotless).  This is a great library and supports mono,
but is based on an older version of Less.  

This library implements a Less compiler using Node.exe. At the moment the library only supports Windows and matches the technique
used in the [Web Essentials Visual Studio extension](https://github.com/madskristensen/WebEssentials2013) as of commit c140cd8b4a8f1012e7e8e140bb7e3db8b49109e3.

To use it, add the DLL to your project and replace the Less configuration with the corresponding LessJs configuration

Old
---

container.Register(Cassette.Stylesheets.ILessCompiler, Cassette.Stylesheets.LessCompiler).AsMultiInstance();
container.Register(typeof(Cassette.IFileSearchModifier<Cassette.Stylesheets.StylesheetBundle>),
                Cassette.Stylesheets.LessFileSearchModifier)).AsSingleton();

New
---

container.Register(Cassette.Stylesheets.ILessJsNodeCompiler, Cassette.Stylesheets.LessJsNodeCompiler).AsMultiInstance();
container.Register(typeof(Cassette.IFileSearchModifier<Cassette.Stylesheets.StylesheetBundle>),
                Cassette.Stylesheets.LessJsNodeFileSearchModifier)).AsSingleton();
