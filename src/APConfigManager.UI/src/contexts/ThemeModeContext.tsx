import { createContext, useContext } from 'react';

export type ThemeMode = 'dark' | 'light';

export const ThemeModeContext = createContext<{
    mode: ThemeMode;
    setMode: (m: ThemeMode) => void;
}>({ mode: 'dark', setMode: () => {} });

export const useThemeMode = () => useContext(ThemeModeContext);