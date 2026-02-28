const express = require('express');
const multer = require('multer');

process.on('uncaughtException', (err) => {
    console.error('Uncaught Exception:', err);
});

const { createProxyMiddleware } = require('http-proxy-middleware');
const path = require('path');
const cors = require('cors');
const fs = require('fs');

// Load config: prioritize external file if packaged
let config;
const externalConfigPath = path.join(path.dirname(process.execPath), 'config.json');

// Check if we are running in a packaged environment or just want to prioritize local file next to executable
if (fs.existsSync(externalConfigPath)) {
    try {
        config = JSON.parse(fs.readFileSync(externalConfigPath, 'utf8'));
        console.log('Using external config.json');
    } catch (e) {
        console.error('Failed to parse external config.json, falling back to internal', e);
        config = require('./config.json');
    }
} else {
    // Fallback for development (or if external file is missing)
    config = require('./config.json');
}

const app = express();
const PORT = 3000;
const upload = multer(); // Memory storage for uploaded files

// Enable CORS for all routes (useful if frontend is on a different port)
app.use(cors());
app.use(express.json({ limit: '200mb' }));
app.use(express.urlencoded({ limit: '200mb', extended: true }));

// 2. Proxy Configuration (Template)
// Proxy to real backend for /plan-quality-metrics/statistics
app.use('/api', createProxyMiddleware({
    target: config.backendUrl, // Your real backend from config
    changeOrigin: true,
    pathRewrite: {
        '^/api': '', // Remove /api prefix when forwarding
    },
}));

// 3. Serve Static Frontend Files
app.use(express.static(path.join(__dirname, '.')));

const srv = app.listen(PORT, () => {
    console.log(`Server is running at http://localhost:${PORT}`);
    console.log(`- Frontend: http://localhost:${PORT}/index.html`);
    console.log(`- Mock API: POST http://localhost:${PORT}/api/analyze`);
});