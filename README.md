# Work-Environment
Repository contains a bunch of command line shortcuts for my typical development actions.

## Usage

Clone this repo, customize configuration at `.config.json` and run `build.bat` to get all the commands in `bin` folder. Add it to path for simplicity then.
File `clean.bat` can be used for build artifacts removal. List of commands is defined in `src/define-commands.fsx` and could be extended quite simply.


## Available commands

* __start-vs__:
  Starts Visual Studio, opening solution, specified in configuration.

* __kill-vs__:
  Closes Visual Studio by killing its process.

* __restart-vs__:
  Restarts Visual Studio, opening solution, specified in configuration.

* __build__:
  Builds backend by running `buildTpAll.backend.tuned.cmd` in solution folder.

* __clean__:
  Removes solution build artifacts.

* __rebuild__:
  Deletes all artifacts and builds solution from scratch.

* __watcher__:
  Starts webpack watcher by running `runWatcher.cmd` in solution folder.

* __github__:
  Opens Github for current git branch. Opens page for branch pull-request, if exists; otherwise branch page itself.
  
* __tp__:
  Opens TargetProcess entity view for current git branch.

* __update-db__:
  Updates database by running `createDatabase.cmd` with db-name parameter in solution folder.

* __recreate-db__:
  Creates database by running `createDatabase.cmd` in solution folder.
