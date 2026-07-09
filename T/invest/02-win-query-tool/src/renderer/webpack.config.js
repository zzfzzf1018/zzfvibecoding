const path = require('path');

module.exports = {
    entry: './src/index.jsx',
    output: {
        filename: 'renderer.js',
        path: path.resolve(__dirname, '../../public/dist')
    },
    module: {
        rules: [
            {
                test: /\.(js|jsx)$/,
                exclude: /node_modules/,
                use: {
                    loader: 'babel-loader'
                }
            },
            {
                test: /\.css$/,
                use: ['style-loader', 'css-loader']
            }
        ]
    },
    resolve: {
        extensions: ['.js', '.jsx']
    },
    devServer: {
        contentBase: path.join(__dirname, '../../public'),
        port: 3000,
        hot: true,
        historyApiFallback: true
    },
    target: 'electron-renderer'
};
