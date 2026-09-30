import { createDarkTheme, createLightTheme, BrandVariants } from '@fluentui/react-components';

const brandColors: BrandVariants = {
  10: '#001F2E',
  20: '#003044',
  30: '#00425B',
  40: '#005473',
  50: '#00678B',
  60: '#007AA4',
  70: '#008EBE',
  80: '#00A2D8',
  90: '#00B7F2',
  100: '#1EC5FF',
  110: '#4DD0FF',
  120: '#70DAFF',
  130: '#8FE3FF',
  140: '#ABEBFF',
  150: '#C5F2FF',
  160: '#DEF8FF',
};

// Dark theme: a touch darker than Fluent's default neutral surfaces, with a
// slight cool tint to sit well with the teal brand.
export const darkTheme = {
    ...createDarkTheme(brandColors),
    colorNeutralBackground1: '#1e1e24',
    colorNeutralBackground1Hover: '#26262e',
    colorNeutralBackground1Pressed: '#181820',
    colorNeutralBackground1Selected: '#26262e',
    colorNeutralBackground2: '#252530',
    colorNeutralBackground3: '#2c2c38',
};

// Light theme: a soft light-gray canvas instead of pure white (easier on the
// eyes), with near-white raised cards.
export const lightTheme = {
    ...createLightTheme(brandColors),
    colorNeutralBackground1: '#e6e6ea',
    colorNeutralBackground1Hover: '#dcdce2',
    colorNeutralBackground1Pressed: '#d2d2d9',
    colorNeutralBackground1Selected: '#dcdce2',
    colorNeutralBackground2: '#f2f2f5',
    colorNeutralBackground3: '#dbdbe1',
};

