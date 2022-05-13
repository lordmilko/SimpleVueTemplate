# SimpleVueTemplate

This repo provides a Visual Studio 2019 template for creating a simple, *fast* Vue website with an ASP.NET Core API backend.

Getting to the point where you can create such a project is far from straightforward. There is a plethora of outdated information out there on setting up such a project; even
the instructions you're following to seem to work, you may find the F5 debug experience to be painfully slow. Anything less than near instantaneous debugging is completely unacceptable. This project is the result of my research.

After creating a new project from this template, you will have a ready to use, quick to debug .NET 5.0/Vue 3 website

## What's in the box?

* ASP.NET Core Empty project targeting .NET 5.0, without HTTPS
* `vue create clientapp --no-git` using the Vue 3 template and the folder renamed to ClientApp
* `Startup.cs` configured to use controllers and your Vue SPA App
* Lightweight, fast, ASP.NET Core Vue Middleware capable of using Hot Module Reload across debugger restarts
* `*.csproj` configured with minimal dependencies and the standard Vue/npm MSBuild targets

## Options

* If you want to use a Vue project with different options (e.g. you want to use TypeScript), simply delete the entire `ClientApp` folder and do `vue create clientapp` in your project (not solution) directory (I changed the capitalization of this folder so it looks nice, but you don't have to)
* As described below, Hot Module Reload does not work across debugger restarts when reading from the `npm` server's console (which is what all other Vue ASP.NET Core Middleware solutions do). As such, by default `SimpleVueTemplate` simply polls to
see when the `npm` web server has come up. If you are experiencing issues with your web server however and want to see what's going on, kill `node` if it's already running and then change your launch target from *IIS Express* to *YourProjectName* and specify `redirectServerOutput` to `UseVueDevelopmentServer()` in `Startup.cs`

## Observations

* [VueCliMiddleware](https://github.com/EEParker/aspnetcore-vueclimiddleware) seems like it'd be the way to go, however it seems to be way slower than [this](https://www.c-sharpcorner.com/article/getting-started-with-vue-js-and-net-core-32/)
* Webpack similarly slows the F5 process down.
* `UseProxyToSpaDevelopmentServer` has a 2000ms delay if you try and connect to `localhost` since it actually tries IPv6 first; therefore you need to specify `127.0.0.1` to force IPv4 (VueCliMiddleware added this as well in 2020, however older VSIX's on the marketplace such as [this](https://marketplace.visualstudio.com/items?itemName=alexandredotnet.netcorevuejs) are still using VueCliMiddleware 3.0 hence are affected by this issue)
* The way Vue ASP.NET Core Middleware typically works is that you connect to the stdin/out/err of `npm run serve` to detect when the backend server has properly started. However, if you
    * Stop the Visual Studio debugger (after having already started it)
    * Start the Visual Studio debugger again (thereby reattaching to the existing `npm run serve` that is still running)
    * And then modify a `*.vue` file
  You will find that Hot Module Reload no longer appears to work. If you then refresh your webpage, node.js will in fact terminate itself, requiring the ASP.NET Core Proxy to recreate the server! While I don't know exactly why this happens (it isn't crashing), it's definitely got something to do with the fact our middleware attached itself to the input/output streams of the npm server. As I haven't been able to find an easy way around this, my solution is to provide the option to simply not connect to the server's input/output streams at all
* [You may want to consider enabling the Script Debugger](https://www.reddit.com/r/dotnet/comments/unzi8b/psa_the_more_windows_you_have_open_the_longer_it/) to make Visual Studio launch your web browser faster. You may find that the Script Debugger causes its own annoyances...but in any case this this is something to be aware of if you keep a lot of open windows
* The Visual Studio Project Template you export from existing code can't simply be incorporated into a VSIX; is it because a `TemplateID` is not specified?
* Files in your project template won't be extracted unless you specify `<CreateInPlace>true</CreateInPlace>` in the vstemplate
