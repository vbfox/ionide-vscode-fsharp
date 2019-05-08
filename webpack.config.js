var path = require("path");
var webpack = require("webpack");

function resolve(filePath) {
  return path.join(__dirname, filePath)
}

var babelOptions = {
  presets: [
    [
      "@babel/preset-env",
      {
        targets: {
          node: "10.2.0" // Found in VSCode about box
        }
      }
    ]
  ],
  plugins: [
    "@babel/plugin-transform-runtime"
  ]
};

var isProduction = process.argv.indexOf("-p") >= 0;
console.log("Bundling for " + (isProduction ? "production" : "development") + "...");

module.exports = function(env) {

  var ionideExperimental = (env && env.ionideExperimental);
  var outputPath = ionideExperimental? "release-exp" : "release";
  console.log("Output path: " + outputPath);

  var compilerDefines = isProduction ? [] : ["DEBUG"];
  if (ionideExperimental) {
    compilerDefines.push("IONIDE_EXPERIMENTAL");
  }

  return {
  target: 'node',
  devtool: "source-map",
  entry: resolve('./src/Ionide.FSharp.fsproj'),
  output: {
    filename: 'fsharp.js',
    path: resolve('./' + outputPath),
    libraryTarget: 'commonjs'
  },
  externals: {
    // Who came first the host or the plugin ?
    "vscode": "commonjs vscode",

    // Optional dependencies of ws
    "utf-8-validate": "commonjs utf-8-validate",
    "bufferutil": "commonjs bufferutil"
  },
  module: {
    rules: [
      {
        test: /\.fs(x|proj)?$/,
        use: {
          loader: "fable-loader",
          options: {
            babel: babelOptions,
            define: compilerDefines
          }
        }
      },
      {
        test: /\.js$/,
        exclude: /node_modules/,
        use: {
          loader: 'babel-loader',
          options: babelOptions
        },
      }
    ]
  }
};
}
