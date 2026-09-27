interface Props {
    percent: number;
    size?: number;
    stroke?: number;
}

/**
 * Circular progress ring with the percentage shown in the center.
 */
export const CircularProgress = ({ percent, size = 90, stroke = 8 }: Props) => {
    const clamped = Math.max(0, Math.min(100, percent));
    const radius = (size - stroke) / 2;
    const circumference = 2 * Math.PI * radius;
    const offset = circumference * (1 - clamped / 100);
    const center = size / 2;

    return (
        <svg width={size} height={size} viewBox={`0 0 ${size} ${size}`} role="img"
             aria-label={`${Math.round(clamped)}%`}>
            <circle
                cx={center}
                cy={center}
                r={radius}
                fill="none"
                stroke="var(--colorNeutralStroke2)"
                strokeWidth={stroke}
            />
            <circle
                cx={center}
                cy={center}
                r={radius}
                fill="none"
                stroke="var(--colorBrandForeground1)"
                strokeWidth={stroke}
                strokeLinecap="round"
                strokeDasharray={circumference}
                strokeDashoffset={offset}
                transform={`rotate(-90 ${center} ${center})`}
                style={{ transition: 'stroke-dashoffset 0.3s ease' }}
            />
            <text
                x="50%"
                y="50%"
                textAnchor="middle"
                dominantBaseline="central"
                fontSize={size * 0.24}
                fontWeight={600}
                fill="var(--colorBrandForeground1)"
            >
                {Math.round(clamped)}%
            </text>
        </svg>
    );
};