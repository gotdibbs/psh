const fs = require('fs');
const keytar = require('keytar');
const path = require('path');
const readline = require('readline');
const shell = require('shelljs');

const commands = process.argv && process.argv.length ? process.argv.slice(2) : [];

const cwd = shell.pwd().toString();
const filePath = path.join(cwd, 'psh.json');

function getConnectionString(config) {
    return new Promise((resolve, reject) => {
        const rl = readline.createInterface({
            input: process.stdin,
            output: process.stdout
        });

        rl.question('Connection String: ', (result) => {
            if (!result) {
                reject('Connection String is required. Cannot continue.');
                return;
            }

            keytar.setPassword('d365.psh', config.connectionName, result).then(() => {
                config.connectionString = result;

                resolve(config);
            }, (e) => {
                console.error('Error encountered storing connection string.');
            });
        });
    });
}

function getConfig () {
    // If the config file doesn't exist...
    if (!fs.existsSync(filePath)) {
        return Promise.reject('No configuration file found. Please create a `psh.json` file at the root of the directory you wish to run `psh` from.');
    }
    
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

    return new Promise((resolve, reject) => {
        return keytar.getPassword('d365.psh', config.connectionName).then((connectionString) => {
            if (connectionString && commands.indexOf('reset') === -1) {
                config.connectionString = connectionString;

                resolve(config);
                return;
            }
            else {
                resolve(getConnectionString(config));
            }
        }, (e) => {
            reject('Error encountered accessing credential store.');
        });
    });
}

module.exports = getConfig;