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

export const darkTheme = { ...createDarkTheme(brandColors) };
export const lightTheme = { ...createLightTheme(brandColors) };

