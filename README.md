# Prerequisites

1. Visual Studio with Visual C++ enabled/installed
2. Run `npm i -g node-gyp`
3. From an administrative command prompt, run `npm i -g --production windows-build-tools` for windows or ensure prerequisites for `node-gyp` are installed another way

## Common Install Issues

 - If you receive an error something to the effect of "cannot find 'CL.EXE'" this is likely because your Visual Studio install did not include C++. Please try going to add/remove programs, Visual Studio, Change, and ensure the C++ feature set is checked off.
 - If you receive an error from edge.js about not having the right node version, just check the npm page for the edge package. The version number of edge should match the version number of node that is required.
 - If #3 from the prerequisites fails on Windows, make sure you're command prompt is running as an administrator.

# Configuration

1. Navigate to the root director of your project
2. Create a new file called `psh.json`
3. The file should contain a json object with these three properties:
 - `root`: The root folder, relative to the current folder, that you wish to push web resources from. Ex. `dist`.
 - `rootNamespace` `[Optional]`: The root namespace to be used for all web resources. For example, if your resource would normally be named `new_/test.js` and you want it to be `new_/CustomUI/test.js` you would specify `CustomUI` for the `rootNamespace` option.
 - `connectionName`: Unique identifier of the connection string that will be stored in your credential manager. This can be reused across projects. The first time you run `psh` it will prompty you to specify a valid D365 connection string to store under this name. Example value to be entered when prompted: `AuthType=Office365;Url=https://orgname.crm.dynamics.com/;Username=wdibbern@example.onmicrosoft.com;Password=somethingsecure;RequireNewInstance=true`. Note that in VS Code you can paste this setting directly into the terminal when prompted.
 - `solutionName`: The unique name of the solution the associated web resources will be pushed to.

## Example Configuration:

```
{
    "root": "dist",
    "connectionName": "FryDev",
    "solutionName": "SlurmFactory"
}
```

## Individual Overrides

By default the utility will build the names of your web resources based on their relative paths to the root directory specified in the configuration file. If you'd like to override this default name, follow the steps below.

1. Adjacent to the file you want to override settings for, create a file with the same name, but with `.psh` added to the end. So if your file is `contact.js` your override file would be `contact.js.psh`.
2. The file should contain a json object with these properties:
 - `namespace`: This should be the desired `name` for your web resource. For example, if by default your file would have been pushed as `nibbler_/contact.js` and you wanted it to be `nibbler_/Scripts/Forms/contact.js`, your value for `namespace` would be `/Scripts/Forms/contact.js`.
 - `description` `[Optional]`: A description to be attached to the web resource record in Dynamics.

# Invocation

From a command prompt set to the same directory as your `psh.json` file, run `psh`. For more control, see the optional arguments below. Note that any of the following optional arguments can be combined.

```
psh [init] [reset] [test] [verbose] [f=[file list]]
```

Optional arguments:

 - `init`: if no `psh.json` file exists at the current working directory, `psh` will attempt to create one and guide you through specifying the required settings.
 - `reset`: allows you to update the stored connection string. Ex. `push reset`.
 - `test`: reports exactly what the utility plans to do, without actually creating, updating, or publishing anything in Dynamics. Ex. `push test`.
 - `verbose`: displays the configuration object to be passed to the C# module. Helpful to check your settings, but note that your connection string will be displayed (may include your password). Ex. `psh verbose`.
 - `f=[file]` or `f=[file1],[file2]`: allows you to push only a subset of files to Dynamics 365. For example, if you only wanted to push the file `dist\js\bundle.js` and your root folder was set to `dist` you would run `psh f=js\bundle.js`.

 An example with everything combined would be entirely valid and would be:
 `psh test verbose f=contact.js reset`

 This would first prompt you to update your connection string, then log out the configuration object, and then a log would display showing that the utility is trying to create/update only `contact.js` from your root folder.