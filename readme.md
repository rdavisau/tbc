# tbc

tbc facilitates patch-based c# hot reload for people who like hard work (and dependency injection, probably).

it's alpha quality by me for me

[![Here's a video](https://i.imgur.com/IljtZDz.png)](https://ryandavisau.blob.core.windows.net/store/teebeecee.mp4?sp=r&st=2021-02-15T07:34:52Z&se=2026-01-31T15:34:52Z&spr=https&sv=2019-12-12&sr=b&sig=eW2DWnMw162dTqEN7DIAzrB6J6MdRWIBZKoRfO32XF8%3D "Here's a video")

# features

## elevator pitch
- **configurable c# 'hot reload'**: patch new types into the running application - new views, viewmodels, services or whatever you can work into the constraints of your application or framework's architecture

- **arbitrary debug/dev commands**: define commands in your application and they can be invoked externally to modify or introspect the application. for example, 'goto to screen', 'log out', 'clear cache', 'list cache' 

- **debuggable reloaded code**: set a breakpoint in code you just reloaded and it will be hit

- **reloadable reloading**: your customised reload implementation and command handler can be reloaded and debugged too.

## in practice: what it does
- 'host' watches for changes in your project 
- 'host' compiles the incremental changes you make into teeny tiny assemblies
- 'host' send the assemblies to your running app (the 'target'), which calls to your `IReloadManager` implementation 

## in practice: what it doesn't do
- actually integrate your changes or perform any kind of reload (that's your job - implement `ProcessNewAssembly` and make magic)
- any of fairly typical quality of life things you might expect from a hot reload framework - service discovery, an ide plugin, etc. 
- respond well to changing static types, extension methods etc. basically if you work outside the known constraints things will probably stop working (but you can use `tree remove` to exclude things.. see below)

# this sounds lame, magic method replacement is the past, present, and future of hot reload! also i want an ide plugin!
Yes you're probably right - you can try [LiveSharp](https://www.livesharp.net/) which is more mature, very configurable, and by now pretty stable (especially for non-Mono workloads). Or you can wait for official dotnet stuff. For another 'type replacement' based reloader you can check out Clancey's [Reloadify 3000](https://github.com/Clancey/Reloadify3000), which does have an IDE plugin and service discovery and is generally going to be easier to use.

tbc will probably always have rough edges/require 'hard work' because an official solution based on a superior implementation is 'coming soon'; it's just a stopgap I have hacked on a bit every time I started a new app because I still need to build things now. If you also need to build things now and don't mind getting hands dirty then tbc could help you too. 

# how to use

1. run a tbc 'host' e.g. `tbc.host.console` from the releases page [*1]. see the video above, I like to run it from the IDE's integrated terminal

2. add the tbc 'target' package to your reloadable app, for now it's a prerelease on MyGet (`tbc.target` in https://www.myget.org/F/rdavisau/api/v3/index.json)

3. provide an implementation of `IReloadManager` (tip: derive from `ReloadManagerBase`)
    - implement `ProcessNewAssembly` to integrate new types and perform any reload/navigation/recall of changed classes
    - implement `ExecuteCommand` to handle arbitrary commands you might want to issue to the running application

   you can see an example of a basic prism reloader in the samples.

4. somewhere near startup, put something like: 
    ``` 
    Task.Run(async () => 
    {
        var reloadManager = ... ; // create or resolve your IReloadManager
        var reloadService = new TargetServer(TargetConfiguration.Default());

        await reloadService.Run(reloadManager);
    });
    ```

5. start making changes! (save to update)


[*1] right now there is only a console host that watches addresses specified by config, but you could imagine an ide extension replicating the same small set of user interface functionality but with broadcast etc. You could use `tbc.host.console` to hot reload building `tbc.host.visualstudio` or `tbc.host.vs4mac` (kinda like [here](https://twitter.com/rdavis_au/status/1179537272380649472)).

# more detail please 

## hot reloading with `ProcessNewAssembly`

`ProcessNewAssembly` is called every time the host stages a new change. You are given a new assembly that contains a new version of all the types changed since the reloadable app started running (or, since the last `reset` command was issued to the host). To 'hot reload' you typically need to perform two steps:

1. Integrate changes
2. 'Reload'  

### integrating changes

To integrate changes you should reflect over the types in the assembly and work them into the running app. For example, in a XF Prism app this would involve registering new services, pages and viewmodels into the container, which you could do in the below manner:

![](https://ryandavis.io/content/images/2019/05/hotpatch-prism-1.png)
_(note that in `ProcessNewAssembly` you receive an already loaded assembly - no need to load from bytes)_

### reloading

Reloading usually amounts to updating the screen with changes you have made. An easy way to achieve this is to navigate to the new type that was introduced. In the Prism example (given you registered the page already) that would involve invoking the navigator as normal. Navigating without animating will give the appearance of instantaneous reload - hurrah, you just made a hot reload!

### going further

Even though tbc isn't truly changing types, with full control over the reload process you can also approximate more sophisticated integrations. For example, you could preserve state between changes by passing a viewmodel between new iterations of a page (or, if the viewmodel is also being reloaded, reflection properties from the old instance onto the new instance). Or, you could replace the cell template on the currently displayed screen if the relevant cell type was part of the reloaded assembly. 

The host may provide a hint as to which type in a new assembly is intended to be the 'primary' one via the `PrimaryType` property. In an application with a UI, you might then make sure the primary type is the one displayed on the screen after reloading. Or, you could ignore the type hint and instead interrogate the current app state (e.g. check what screen is currently being displayed) to decide what the best action to take is.

## commands

tbc includes the concept of 'commands' which can be issued either to the host or the target. When using the console host, you can type commands directly into the console to send them. 

### host commands
The host supports a small set of commands that were useful to me:

`primary {hint}`: specify that one of the types in the staged changes should be the 'primary' type. You can provide just a hint of the type name and the host will attempt to match (e.g. `primary home` will resolve the `HomePage` class if it is part of the staged changes)

`trees`: prints the set of staged files and contained types. Useful for seeing what incremental is being tracked.

`tree remove {hint}`: removes a file from the staged set. Useful if you change a file that doesn't play nice with tbc (e.g. a static class with extension methods) and want to un-break reload without having to restart things.

`reset`: resets the state of the incremental compiler - removes all trees and references, and asks the target to resend dependencies. 

### target commands

Commands prefixed with '!' will be sent to targets rather than the host. There are no built-in commands supported by the target, but you can add support for arbitrary commands to your `IReloadManager` by implementing the `ExecuteCommand` method. For example, a `goto` command in a prism app could be implemented using the code below, which walks the service container for pages and allows them to be chosen from a menu (if not specified as an argument to the command):

![](https://i.imgur.com/eIa1QU6.png)

([this command exists in the prism sample](https://github.com/rdavisau/tbc/blob/main/src/samples/prism/tbc.sample.prism/tbc.sample.prism/tbc.sample.prism/ReloadManager.cs#L97-L142))

You could implement other useful debug commands for your application, like login/logout, cache clearing etc. 

Since your `IReloadManager` is itself reloadable (provided it derives from `ReloadManagerBase`), you can add support for new commands while the application is still running. See [here](https://github.com/rdavisau/tbc/blob/main/src/samples/prism/tbc.sample.prism/tbc.sample.prism/tbc.sample.prism/ReloadManager.cs#L37-L47) in the prism sample for how you can make the reloader reloadable.

## debugging

Since the incremental compiler builds directly off the source files you're working on, debugging reloaded code is possible. Nice!

# alpha quality

I've only used this for myself but have used earlier incarnations on production-complexity apps. I've only tested on iOS.

Your mileage may vary. Messing with static classes probably won't work (`tree remove` them 🤠). Xaml files won't work (delete them 🤠🤠).

I used gRPC for the host/target interop. For some reason, to build with iOS you need to add [this](https://github.com/rdavisau/tbc/blob/main/src/samples/prism/tbc.sample.prism/tbc.sample.prism/tbc.sample.prism.iOS/tbc.sample.prism.iOS.csproj#L149-L170) to your csproj. [Maybe it will get fixed](https://github.com/grpc/grpc/issues/19172), maybe I'll swap gRPC for something else. 