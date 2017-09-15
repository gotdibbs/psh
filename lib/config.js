const fs = require('fs');
const keytar = require('keytar');
const path = require('path');
const readline = require('readline');
const shell = require('shelljs');

const commands = process.argv && process.argv.length ? process.argv.slice(2) : [];

const cwd = shell.pwd().toString();
const filePath = path.join(cwd, 'psh.json');

const rl = readline.createInterface({
    input: process.stdin,
    output: process.stdout
});

function prompt(text, isRequired) {
    return new Promise((resolve, reject) => {
        rl.question(`${text}: `, (result) => {
            if (!result && isRequired) {
                return reject(`A value for '${text}' is required. Cannot continue.`);
            }

            resolve(result);
        });
    });
}

function getConnectionString(config) {
    return prompt('Connection String', true)
        .then((result) => {
            config.connectionString = result;

            return keytar.setPassword('d365.psh', config.connectionName, config.connectionString);
        })
        .then(() => {
            resolve(config);
        });
}

function createConfig() {
    var config = { root: "", connectionName: "", solutionName: "" }

    return prompt('Root')
        .then(root => {
            config.root = root;

            return prompt('Connection Name');
        })
        .then(connectionName => {
            config.connectionName = connectionName;

            return prompt('Solution Unique Name');
        })
        .then(solutionName => {
            config.solutionName = solutionName;
        })
        .then(() => {
            return new Promise((resolve, reject) => {
                fs.writeFile(filePath, JSON.stringify(config, null, 4), function(err) {
                    if (err) {
                        return reject(err);
                    }
                
                    resolve();
                });
            });
        });
}

function parseConfig() {
    const config = require(filePath);
    
    // If no resource configs are defined...
    if (!config || !config.root || !config.connectionName) {
        return Promise.reject('Invalid configuration found. Please review the documentation and ensure your `psh.json` file is configured correctly.');
    }

    config.path = path.resolve(cwd, config.root);

    config.dryRun = (commands.indexOf('test') !== -1);
    config.verbose = (commands.indexOf('verbose') !== -1);
    var limitedFiles = commands.find(c => c != null && /^f=.+$/.test(c));

    if (limitedFiles) {
        config.files = limitedFiles.split('f=')[1].split(',');
    }
    else {
        config.files = null;
    }

    return keytar.getPassword('d365.psh', config.connectionName).then((connectionString) => {
        if (connectionString && commands.indexOf('reset') === -1) {
            config.connectionString = connectionString;

            return config;
        }
        else {
            return getConnectionString(config);
        }
    }, (e) => {
        console.error('Error encountered accessing credential store.');
    });
}

function getConfig () {
    if (!fs.existsSync(filePath)) {
        if (commands.indexOf('init') !== -1) {
            return createConfig().then(parseConfig);
        }
        else {
            return Promise.reject('No configuration file found. Please run `psh init` or create a `psh.json` file at the root of the directory you wish to run `psh` from.');
        }
    }
    
    return parseConfig();
}

module.exports = getConfig;