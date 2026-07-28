import { app, BrowserWindow, Tray, Menu, nativeImage, ipcMain } from 'electron';
import path from 'path';

export function createTray(mainWindow: BrowserWindow | null): Tray {
  const iconPath = path.join(__dirname, '../../icon.ico');
  const icon = nativeImage.createFromPath(iconPath);
  const tray = new Tray(icon.resize({ width: 16, height: 16 }));

  let protectionStatus = 'Initializing...';

  ipcMain.on('tray:protection-status', (_event, status: string) => {
    protectionStatus = status;
    updateMenu();
  });

  const updateMenu = () => {
    const contextMenu = Menu.buildFromTemplate([
      {
        label: 'Show Anti-Cheat',
        click: () => {
          if (mainWindow) {
            mainWindow.show();
            mainWindow.focus();
          }
        },
      },
      { type: 'separator' },
      {
        label: `Protection: ${protectionStatus}`,
        enabled: false,
      },
      { type: 'separator' },
      {
        label: 'Quit',
        click: () => {
          app.quit();
        },
      },
    ]);

    tray.setContextMenu(contextMenu);
  };

  tray.setToolTip('Mafia City Anti-Cheat V6');
  updateMenu();

  tray.on('double-click', () => {
    if (mainWindow) {
      mainWindow.show();
      mainWindow.focus();
    }
  });

  return tray;
}
