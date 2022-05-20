# SimpleVueTemplate

[![Appveyor status](https://ci.appveyor.com/api/projects/status/8psqq59hww01ipla?svg=true)](https://ci.appveyor.com/project/lordmilko/simplevuetemplate)

This repo provides a Visual Studio 2019 template for creating a simple, *fast* Vue website with an ASP.NET Core API backend.

Getting to the point where you can create such a project is far from straightforward. There is a plethora of outdated information out there on setting up such a project; even
the instructions you're following to seem to work, you may find the F5 debug experience to be painfully slow. Anything less than near instantaneous debugging is completely unacceptable. This project is the result of my research.

After creating a new project from this template, you will have a ready to use, quick to debug .NET 5.0/Vue 3 website

## What's in the box?

* ASP.NET Core Empty project targeting .NET 5.0, without HTTPS
* `vue create clientapp --no-git` using the Vue 3 template and the folder renamed to ClientApp
* `Startup.cs` configured to use controllers and your Vue SPA App
* Lightweight, fast, ASP.NET Core Vue Middleware capable of using Hot Reload across debugger restarts
* `*.csproj` configured with minimal dependencies and the standard Vue/npm MSBuild targets

## Options

* If you want to use a Vue project with different options (e.g. you want to use TypeScript), simply delete the entire `ClientApp` folder and do `vue create clientapp` in your project (not solution) directory (I changed the capitalization of this folder so it looks nice, but you don't have to)
* As described below, Hot Reload does not work across debugger restarts when reading from the `npm` server's console (which is what all other Vue ASP.NET Core Middleware solutions do). As such, by default `SimpleVueTemplate` simply polls to
see when the `npm` web server has come up. If you are experiencing issues with your web server however and want to see what's going on, kill `node` if it's already running and then change your launch target from *IIS Express* to *YourProjectName* and specify `VueServeMode.Parse` to `UseVueDevelopmentServer()` in `Startup.cs`
* If you attempt to debug this project in Visual Studio Code, setting `VueServeMode.Poll` will cause `npm run serve` to run in the integrated terminal window, thereby causing Hot Reload issues if you close and reopen Visual Studio Code. To work around this,
  either run `npm run serve` manually before debugging, or if you are on Windows you may specify `VueServeMode.Isolated` to `UseVueDevelopmentServer`, which will cause a brand new window to open (minimised) containing the running node server.

## Observations

### ASP.NET Core

* [VueCliMiddleware](https://github.com/EEParker/aspnetcore-vueclimiddleware) seems like it'd be the way to go, however it seems to be way slower than [this](https://www.c-sharpcorner.com/article/getting-started-with-vue-js-and-net-core-32/)
* Webpack similarly slows the F5 process down.
* `UseProxyToSpaDevelopmentServer` has a 2000ms delay if you try and connect to `localhost` since it actually tries IPv6 first; therefore you need to specify `127.0.0.1` to force IPv4 (VueCliMiddleware added this as well in 2020, however older VSIX's on the marketplace such as [this](https://marketplace.visualstudio.com/items?itemName=alexandredotnet.netcorevuejs) are still using VueCliMiddleware 3.0 hence are affected by this issue)
* The way Vue ASP.NET Core Middleware typically works is that you connect to the stdin/out/err of `npm run serve` to detect when the backend server has properly started. However, if you
    * Stop the Visual Studio debugger (after having already started it)
    * Start the Visual Studio debugger again (thereby reattaching to the existing `npm run serve` that is still running)
    * And then modify a `*.vue` file
  You will find that Hot Reload no longer appears to work. If you then refresh your webpage, node.js will in fact terminate itself, requiring the ASP.NET Core Proxy to recreate the server! While I don't know exactly why this happens (it isn't crashing), it's definitely got something to do with the fact our middleware attached itself to the input/output streams of the npm server. As I haven't been able to find an easy way around this, my solution is to provide the option to simply not connect to the server's input/output streams at all
* [You may want to consider enabling the Script Debugger](https://www.reddit.com/r/dotnet/comments/unzi8b/psa_the_more_windows_you_have_open_the_longer_it/) to make Visual Studio launch your web browser faster. You may find that the Script Debugger causes its own annoyances...but in any case this this is something to be aware of if you keep a lot of open windows

### VSIX Development

* The Visual Studio Project Template you export from existing code can't simply be incorporated into a VSIX; is it because a `TemplateID` is not specified?
* Files in your project template won't be extracted unless you specify `<CreateInPlace>true</CreateInPlace>` in the vstemplate
* Microsoft really wants you to strongly name the assembly containing your `IWizard`. If you don't want to do that though, you need to ensure that the assembly you specify in your `*.vstemplate` `<WizardExtension>` matches the assembly
  specified in the corresponding `<Asset>` of `source.extension.vsixmanifest`. By default the manifest contains `<Asset AssemblyName=|%CurrentProject%;AssemblyName|"/>`which expands to the strong name of your assembly. [Microsoft hasn't documented)[https://github.com/MicrosoftDocs/visualstudio-docs/issues/820]
  what possible macros can be specified in manifests, however with a quick bit of reverse engineering we can see that targets defined in `Microsoft.VsSDK.targets` can be specified, and `VSIXNameProjectOutputGroup` gives us our project name without a file extension. Alternatively, you can just hardcode your assembly name in the manifest
* .NET project types have a fancy second screen in Visual Studio 2019 where you can enter in values such as the .NET Framework version, whether to use HTTPS, etc. Based on this you may think it's possible to create fancy integrated wizards rather than having to design
  your own `IWizard` interface, however this is not the case. It is possible for .NET Core templates packaged up in a VSIX to show additional options on the main window, however these options are only used to perform simple substitutions within your source text; you can't do something more
  complex like run an arbitrary program to provide a list of possible options for a dropdown list.
  
  Why is it not possible to design your own fancy integrated wizard like the one .NET projects do?
  * Templates are instantiated via an `ITemplateProvider`. The two main template providers used in Visual Studio 2019 are `LegacyProjectTemplateProvider` (from `Microsoft.VisualStudio.Dialogs.dll`) and `NetTemplateProvider` (from `Microsoft.VisualStudio.TemplateEngine.VS.dll`)
  * In order for a `Template` to display a secondary configuration page within the normal new project screen, `HasInputDescriptor` must be true. This is never the case for templates created from the `LegacyVsTemplateProvider`
  * Secondly, the `ITemplateProvider.GetTemplateInputDescriptorAsync` must return a `TemplateInputsDescriptor` containing a list of `ControlDescription` objects obtained from an `ITemplateParameterProvider`. This interface is not public hence it is not possible to add your own control descriptors
  * In that case then, is it possible to trick Visual Studio into reading your template via the `NetTemplateProvider` instead of `LegacyProjectTemplateProvider`? `NetTemplateDialogFilter` (from `Microsoft.VisualStudio.TemplateEngine.VS.dll`) declares all well known template types. If you're not in the list, you don't get to display additional input fields
  * [The only possible out](https://github.com/dotnet/templating/issues/1987) for you is if you do [declare](https://github.com/ligershark/sidewafflev2/blob/master/templates/SideWaffle.Template/.template.config/sidewaffle.vstemplate) a wizard in your vstemplate, but point to `Microsoft.VisualStudio.TemplateEngine.Wizard.TemplateEngineWizard` instead of an actual template a `none` `$uistyle$` and either a `$templateid$` or `$groupid$`. The only issue here is that Visual Studio will lookup whatever `$templateid$` or `$groupid$` you specify
    against a list of known IDs that comes from somewhere. Your extension didn't declare either of those two properly (and in fact I think you need a `.template.config\template.json` .NET Core template file to do so), so your superhack fails. sidewafflev2 does seem to know how to declare its group properly somehow
  * But even if you did manage to trick Visual Studio into allowing custom inputs for your project, it's still completely pointless, because all known input controls are provided via a series of predefined `ITemplateParameterProvider` types, which magically come from somewhere via MEF. You would need to do some crazy
    dynamic programming to create an instance of a type derived from `ITemplateParameterProvider`, and even then you have to deal with a whole bunch of MEF stuff. No thanks