import UAParser from 'ua-parser-js';


export const getParser = () => {
  let ua = navigator.userAgent;
  return new UAParser(ua);
};

export const getPlatform = () => {
  return getParser().getOS().name;
};

export const getBrowser = () => {
  return getParser().getResult().browser.name;
};

export const browserInfo = {
  browser: getBrowser(),
  isMobile: getParser().getDevice().type === 'mobile',
  os: getParser().getOS().name,
};

export const isMacOS = () => getPlatform() === 'Mac OS';

export const isInStandaloneMode = () => {
  return (
    window.matchMedia('(display-mode: standalone)').matches ||
    ('standalone' in navigator && (navigator as any).standalone === true)
  );
};

export const isSonomaOrLaterSafari = () => {
  const userAgent = navigator.userAgent.toLowerCase();
  if (navigator.maxTouchPoints || !/macintosh/.test(userAgent)) return false;

  // check safari version >= 17
  const version = /version\/(\d{2})\./.exec(userAgent);
  if (!version || !version[1] || !(parseInt(version[1]) >= 17)) return false;

  try {
    // hacky way to detect Sonoma
    const audioCheck = document.createElement('audio').canPlayType('audio/wav; codecs="1"');
    const webGLCheck = new OffscreenCanvas(1, 1).getContext('webgl');
    return Boolean(audioCheck) && Boolean(webGLCheck);
  } catch {
    return false;
  }
};
