#!/usr/bin/env node
const getConfig = require('./lib/config');
const crm = require('./lib/crm');

// TODO: support iterating through multiple configs
// TODO: ignore file support
// TODO: support minification

getConfig().then((config) => {
    if (config && config.verbose) {
        console.log(config);
    }

    if (config) {
        return crm.push(config);
    }
}).then((result) => {
    console.log(result);

    process.exit();
}).catch((e) => {
    console.error(e);

    process.exit();
});