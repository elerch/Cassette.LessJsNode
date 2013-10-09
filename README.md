Cassette.LessJs
===============

Version of the Less compiler for Cassette.Net using the original Js Libraries

Cassette.Less uses [dotless](https://github.com/dotless/dotless).  This is a great library and supports mono,
but is based on an older version of Less.  

This library implements a Less compiler using the [windows script host version of less.js for windows](https://github.com/duncansmart/less.js-windows/tree/windows-script-host).
This strictly limits usage to Windows OS, but has the advantage of matching the implementation of less compilation
in the [Web Essentials Visual Studio extension](https://github.com/madskristensen/WebEssentials2013).

To use it, add the DLL to your project and replace the Less configuration with the corresponding LessJs configuration

Old
---

container.Register(Cassette.Stylesheets.ILessCompiler, Cassette.Stylesheets.LessCompiler).AsMultiInstance();
container.Register(typeof(Cassette.IFileSearchModifier<Cassette.Stylesheets.StylesheetBundle>),
                Cassette.Stylesheets.LessFileSearchModifier)).AsSingleton();

New
---

container.Register(Cassette.Stylesheets.ILessJsCompiler, Cassette.Stylesheets.LessJsCompiler).AsMultiInstance();
container.Register(typeof(Cassette.IFileSearchModifier<Cassette.Stylesheets.StylesheetBundle>),
                Cassette.Stylesheets.LessJsFileSearchModifier)).AsSingleton();
