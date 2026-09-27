import { useState } from 'react';

interface NavButtonProps {
    label: string;
    active: boolean;
    disabled?: boolean;
    onClick: () => void;
}

/**
 * Top navigation button with a "raise" hover effect (lift + soft shadow +
 * accent border), themed with the app's brand palette. The active tab is filled
 * with the brand color; locked tabs are dimmed and non-interactive.
 */
export const NavButton = ({ label, active, disabled, onClick }: NavButtonProps) => {
    const [hover, setHover] = useState(false);
    const raised = hover && !disabled && !active;

    return (
        <button
            type="button"
            disabled={disabled}
            onClick={onClick}
            onMouseEnter={() => setHover(true)}
            onMouseLeave={() => setHover(false)}
            style={{
                font: 'inherit',
                fontSize: '15px',
                fontWeight: 600,
                lineHeight: 1,
                padding: '9px 20px',
                borderRadius: '8px',
                border: '2px solid',
                cursor: disabled ? 'not-allowed' : 'pointer',
                opacity: disabled ? 0.4 : 1,
                background: active ? 'var(--colorBrandBackground)' : 'transparent',
                borderColor: active
                    ? 'var(--colorBrandBackground)'
                    : raised
                        ? 'var(--colorBrandForeground1)'
                        : 'var(--colorNeutralStroke1)',
                color: active
                    ? 'var(--colorNeutralForegroundOnBrand)'
                    : raised
                        ? 'var(--colorBrandForeground1)'
                        : 'var(--colorNeutralForeground2)',
                transform: raised ? 'translateY(-0.25em)' : 'none',
                boxShadow: raised ? '0 0.5em 0.5em -0.4em rgba(30, 197, 255, 0.65)' : 'none',
                transition: 'transform 0.25s, box-shadow 0.25s, border-color 0.25s, color 0.25s, background 0.25s',
            }}
        >
            {label}
        </button>
    );
};