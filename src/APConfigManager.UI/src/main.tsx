import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';

const globalStyle = document.createElement('style');
globalStyle.textContent = `
  * { margin: 0; padding: 0; box-sizing: border-box; }
  body { overflow: hidden; }
`;
document.head.appendChild(globalStyle);

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <App />
  </React.StrictMode>
);
