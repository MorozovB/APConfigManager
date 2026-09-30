/// <reference types="vite/client" />

// Static image assets imported as URLs (Vite resolves these at build time).
// Vite 8's client types no longer declare these, so declare them explicitly.
declare module '*.png' {
    const src: string;
    export default src;
}
declare module '*.svg' {
    const src: string;
    export default src;
}
declare module '*.jpg' {
    const src: string;
    export default src;
}