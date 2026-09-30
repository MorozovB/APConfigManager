import { useEffect, useState, type ReactNode } from 'react';
import {
    SubtractRegular,
    SquareRegular,
    SquareMultipleRegular,
    DismissRegular,
} from '@fluentui/react-icons';
import logoApp from '../../assets/LogoApp.png';

// Electron's frameless drag regions are non-standard CSS, so extend CSSProperties.
type DragCSS = React.CSSProperties & { WebkitAppRegion?: 'drag' | 'no-drag' };

const barStyle: DragCSS = {
    display: 'flex',
    alignItems: 'center',
    height: '32px',
    flexShrink: 0,
    backgroundColor: 'var(--colorNeutralBackground3)',
    borderBottom: '1px solid var(--colorNeutralStroke1)',
    userSelect: 'none',
    WebkitAppRegion: 'drag',
};

const controlsStyle: DragCSS = {
    display: 'flex',
    height: '100%',
    WebkitAppRegion: 'no-drag',
};

export const TitleBar = () => {
    const api = window.electronAPI?.window;
    const [maximized, setMaximized] = useState(false);

    useEffect(() => {
        if (!api) return;
        let active = true;
        void api.isMaximized().then((m) => { if (active) setMaximized(m); });
        const off = api.onMaximizeChange(setMaximized);
        return () => { active = false; off(); };
    }, [api]);

    // Outside Electron (e.g. plain browser dev) there is no window bridge — render nothing.
    if (!api) return null;

    return (
        <div style={barStyle}>
            {/*Compact logo placeholder (left) — vector art goes here later.*/}
            {/* App logo + title (left)*/}
            <img
                src={logoApp}
                alt="AP Configuration Manager"
                width={38}
                height={20}
                style={{ margin: '0 8px 0 10px', flexShrink: 0 }}
            />
            <span
                style={{
                    fontSize: '13px',
                    fontWeight: 600,
                    whiteSpace: 'nowrap',
                    color: 'var(--colorNeutralForeground1)',
                }}
            >
                AP Configuration Manager
            </span>

            {/* Draggable spacer */}
            <div style={{ flex: 1, height: '100%' }} />

            <div style={controlsStyle}>
                <WinButton title="Minimize" onClick={() => api.minimize()}>
                    <SubtractRegular />
                </WinButton>
                <WinButton title={maximized ? 'Restore' : 'Maximize'} onClick={() => api.toggleMaximize()}>
                    {maximized ? <SquareMultipleRegular /> : <SquareRegular />}
                </WinButton>
                <WinButton title="Close" danger onClick={() => api.close()}>
                    <DismissRegular />
                </WinButton>
            </div>
        </div>
    );
};

interface WinButtonProps {
    title: string;
    onClick: () => void;
    danger?: boolean;
    children: ReactNode;
}

const WinButton = ({ title, onClick, danger, children }: WinButtonProps) => {
    const [hover, setHover] = useState(false);
    return (
        <button
            type="button"
            title={title}
            onClick={onClick}
            onMouseEnter={() => setHover(true)}
            onMouseLeave={() => setHover(false)}
            style={{
                width: '46px',
                height: '100%',
                border: 'none',
                cursor: 'pointer',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                fontSize: '16px',
                color: hover && danger ? '#fff' : 'var(--colorNeutralForeground2)',
                background: hover
                    ? (danger ? '#c42b1c' : 'var(--colorNeutralBackground1Hover)')
                    : 'transparent',
                transition: 'background 0.15s',
            }}
        >
            {children}
        </button>
    );
};