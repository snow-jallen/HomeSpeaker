Instead of using `dotnet run`, use `dotnet watch` to endable hot reloading. 
This allows you to see changes in real-time without needing to restart the application.

If there's a blazor webassembly project and a blazor server project, don't try to run the webassembly project directly. Instead, run the server project which will handle the webassembly project as well.

There should always be a blank line of whitespace between each method.  New html elements should be on a new line.