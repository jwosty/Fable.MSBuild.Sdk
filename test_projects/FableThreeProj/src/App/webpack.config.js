// Note this only includes basic configuration for development mode.
// For a more comprehensive configuration check:
// https://github.com/fable-compiler/webpack-config-template

var path = require("path");

module.exports = (env, argv) => {
    var inputDir = env.inputDir;
    if (inputDir == null) {
        throw (new Error("inputDir must be set"))
    }
    var resolvedInputDir = path.resolve(__dirname, inputDir);
    
    return {
        mode: "development",
        entry: path.resolve(resolvedInputDir, "App.js"),
        output: {
            path: path.join(__dirname, "./public"),
            filename: "bundle.js",
        },
        devServer: {
            static: {
                directory: path.resolve(__dirname, "./public"),
                publicPath: "/",
            },
            port: 8080,
        },
        module: {}
    }
}
