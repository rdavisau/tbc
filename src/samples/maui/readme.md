run run-host.sh to start a host that will watch for the maui sample, then start changing MainPage.cs and friends.

[quick video](https://rdavisau.blob.core.windows.net/zzz/tbc-maui.mp4?sv=2020-08-04&st=2023-01-12T05%3A07%3A25Z&se=2050-01-11T15%3A00%3A00Z&sr=b&sp=r&sig=ukrEaCJ15tyjn5pGd0wdMVxWsamVLLcZ20h6oB0B7lY%3D) - note video has rapid flashing colours for a few seconds at ~3:30 🚦

[android] (https://rdavisau.blob.core.windows.net/zzz/tbc-maui-android.mp4?sv=2020-08-04&st=2023-02-08T02%3A25%3A12Z&se=2050-02-09T02%3A25%3A00Z&sr=b&sp=r&sig=oRvblPXgJWBa6qNsMO8n%2FpCGrzTS6MpLuPccEslx6z0%3D)

on android, you need to `adb forward tcp:50130 tcp:50130` and wait for all dependencies to slowly be shuffled from the app to the host (and maybe one failed reload needs to trigger it?)

howto in general: 

* add tbc package reference
* implement your reload manager
* start target server with your reload manager
* run your app and a tbc host 
* make your changes
  * ui stuff
  * view model stuff
  * services?
  * add commands? when you put !abc in the cli, that gets sent to the reload manager

notable things:

* mutable DI container 
  * in MauiProgram.cs, `ConfigureMutableContainer` sets up the app to use DryIoC as the underlying container. It's good, but also it allows mutation of registrations in the container after it has been created, which we take advantage of for our hot reload things
  
* tbc runs at startup in debug builds
  * in MauiProgram.cs, `RunTbc` sets up tbc to listen in debug builds only

* shared type registration logic
  * in ContainerExtensions.cs, `RegisterApplicationTypes` is called at startup to register types by convention. It is also used in the `ReloadManager` to update registrations with any reloaded/added types

* toy base classes used for demonstrations
  * in BaseTypes.cs there are base types for page, viewmodel, and service. they're used as markers for the auto container registration and to give an idea of how a real app might have it. But the viewmodel isn't even INPC. But it could be

* basic reload manager implementation
  * `ReloadManager.cs` has a basic reload implementation in it. After registering new types, it looks for a page type to resolve and sets that to `App.MainPage`. Another approach could be registering new page types for navigation and then navigating to the preferred page type.

* source generators and global usings
  * they have to be manually specified in reload-config.json (imagine if tbc tried to not be hard to use and determined them for you). in this one there are global usings for ios and source generators from maui markup community toolkit

