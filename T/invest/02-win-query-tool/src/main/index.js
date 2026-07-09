const { app, BrowserWindow, ipcMain, dialog } = require('electron');
const path = require('path');
const { spawn } = require('child_process');
const fs = require('fs');
const http = require('http');

let mainWindow;
let pythonProcess;

function createWindow() {
    mainWindow = new BrowserWindow({
        width: 1200,
        height: 800,
        webPreferences: {
            nodeIntegration: true,
            contextIsolation: false,
            enableRemoteModule: true
        },
        icon: path.join(__dirname, '../../public/icon.ico'),
        title: '股票财务查询',
        show: false
    });

    const htmlPath = path.join(__dirname, '../../public/index.html');
    console.log(`Loading HTML file: ${htmlPath}`);
    console.log(`HTML exists: ${fs.existsSync(htmlPath)}`);

    mainWindow.loadFile(htmlPath);

    mainWindow.webContents.on('did-finish-load', () => {
        console.log('HTML loaded successfully');
    });

    mainWindow.webContents.on('did-fail-load', (event, errorCode, errorDescription, validatedURL, isMainFrame) => {
        console.error(`Load failed: errorCode=${errorCode}, errorDescription=${errorDescription}, URL=${validatedURL}`);
    });

    mainWindow.webContents.on('console-message', (event, level, message, line, sourceId) => {
        console.log(`Renderer console [${level}]: ${message} (${sourceId}:${line})`);
    });

    mainWindow.webContents.on('render-process-gone', (event, details) => {
        console.error(`Renderer process crashed: ${details.reason}`);
    });
    
    mainWindow.on('closed', function () {
        mainWindow = null;
    });
}

function startPythonServer() {
    return new Promise((resolve, reject) => {
        const pythonPath = 'python';
        const scriptPath = path.join(__dirname, '../python/app.py');
        
        console.log(`Starting Python server: ${pythonPath} ${scriptPath}`);
        
        pythonProcess = spawn(pythonPath, [scriptPath]);
        
        pythonProcess.stdout.on('data', (data) => {
            console.log(`Python stdout: ${data}`);
        });
        
        pythonProcess.stderr.on('data', (data) => {
            console.error(`Python stderr: ${data}`);
        });
        
        pythonProcess.on('close', (code) => {
            console.log(`Python process exited with code ${code}`);
            if (mainWindow && !mainWindow.isDestroyed()) {
                dialog.showMessageBox(mainWindow, {
                    type: 'error',
                    title: '服务异常',
                    message: `数据服务已停止，退出码: ${code}`
                }).then(() => {
                    app.quit();
                });
            }
        });

        const maxAttempts = 30;
        let attempts = 0;
        
        const checkReady = () => {
            attempts++;
            console.log(`Checking Python server... attempt ${attempts}/${maxAttempts}`);
            
            if (attempts > maxAttempts) {
                reject(new Error('Python服务启动超时'));
                return;
            }
            
            const req = http.get({
                hostname: '127.0.0.1',
                port: 5000,
                path: '/api/health',
                timeout: 5000
            }, (res) => {
                console.log(`Health check response status: ${res.statusCode}`);
                if (res.statusCode === 200) {
                    let data = '';
                    res.on('data', (chunk) => {
                        data += chunk;
                    });
                    res.on('end', () => {
                        try {
                            const result = JSON.parse(data);
                            console.log(`Python server ready, akshare available: ${result.akshare_available}`);
                            resolve(result);
                        } catch (e) {
                            console.error(`Failed to parse response: ${e.message}`);
                            setTimeout(checkReady, 1000);
                        }
                    });
                } else {
                    setTimeout(checkReady, 1000);
                }
            });
            
            req.on('error', (err) => {
                console.log(`Health check error: ${err.message}, retrying...`);
                setTimeout(checkReady, 1000);
            });
            
            req.on('timeout', () => {
                console.log('Health check timeout, retrying...');
                req.destroy();
                setTimeout(checkReady, 1000);
            });
        };
        
        setTimeout(checkReady, 2000);
    });
}

app.whenReady().then(async () => {
    const userDataPath = path.join(app.getAppPath(), 'user_data');
    fs.mkdirSync(userDataPath, { recursive: true });
    app.setPath('userData', userDataPath);
    
    try {
        console.log('Starting application...');
        createWindow();
        
        const result = await startPythonServer();
        
        if (mainWindow && !mainWindow.isDestroyed()) {
            console.log('Showing main window...');
            mainWindow.show();
            mainWindow.focus();
            console.log('Window shown, isVisible:', mainWindow.isVisible());
            console.log('Window bounds:', mainWindow.getBounds());
            
            if (!result.akshare_available) {
                dialog.showMessageBox(mainWindow, {
                    type: 'warning',
                    title: '警告',
                    message: '未检测到akshare库，请安装依赖后重试'
                });
            }
        }
        
        app.on('activate', function () {
            if (BrowserWindow.getAllWindows().length === 0) {
                createWindow();
            }
        });
    } catch (error) {
        console.error('Failed to start application:', error);
        dialog.showMessageBox({
            type: 'error',
            title: '启动失败',
            message: `无法启动数据服务:\n${error.message}\n\n请确保已安装Python和相关依赖`
        }).then(() => {
            app.quit();
        });
    }
});

app.on('window-all-closed', function () {
    if (pythonProcess) {
        pythonProcess.kill();
    }
    if (process.platform !== 'darwin') {
        app.quit();
    }
});

ipcMain.handle('open-file-dialog', async (event, options) => {
    const result = await dialog.showOpenDialog(mainWindow, options);
    return result;
});

ipcMain.handle('open-save-dialog', async (event, options) => {
    const result = await dialog.showSaveDialog(mainWindow, options);
    return result;
});

ipcMain.handle('show-message', async (event, options) => {
    const result = await dialog.showMessageBox(mainWindow, options);
    return result;
});

ipcMain.handle('open-external', async (event, url) => {
    await require('electron').shell.openExternal(url);
});

ipcMain.handle('download-file', async (event, sourcePath, destPath) => {
    try {
        fs.copyFileSync(sourcePath, destPath);
        return { success: true };
    } catch (error) {
        return { success: false, error: error.message };
    }
});
