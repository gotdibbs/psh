const edge = require('edge');
const path = require('path');

function push(config) {
    var invoke = edge.func(path.join(__dirname, 'Psh.Interface.dll'));

    return new Promise((resolve, reject) => {
        invoke(config, (error, result) => {
            if (error) {
                reject(error);
                return;
            }

            resolve(result);
        });
    });
}

module.exports = {
    push
};