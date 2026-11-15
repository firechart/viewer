// The banner is the face of the telemetry consent; the setting GeneralAppSettings.AllowTelemetry stays the source of truth.
// The library's own cookie only remembers that the user has answered. Imported by TelemetryConsentBanner, only where telemetry is on: the library
// and its styles are loaded here, not at the start of every application
const LIBRARY = '_content/Flox.Shared/lib/cookieconsent/dist/cookieconsent.umd.min.js';
const STYLES = '_content/Flox.Shared/lib/cookieconsent/dist/cookieconsent.min.css';

function load(tag, attributes) {
    return new Promise((resolve, reject) => {
        const element = document.createElement(tag);
        Object.assign(element, attributes);
        element.onload = resolve;
        element.onerror = reject;
        document.head.appendChild(element);
    });
}

async function loadLibrary() {
    if (typeof CookieConsent !== 'undefined') {
        return;
    }

    try {
        await Promise.all([load('script', { src: LIBRARY }), load('link', { rel: 'stylesheet', href: STYLES })]);
    } catch {
        // a content blocker (cookie banner lists): no banner
    }
}

const floxConsent = {
    init: async function (handler, texts, allowed) {
        await loadLibrary();
        if (typeof CookieConsent === 'undefined') {
            console.warn('CookieConsent library not loaded (blocked?): no consent banner');
            return;
        }

        const report = function () {
            handler.invokeMethodAsync('OnTelemetryConsentChanged', CookieConsent.acceptedCategory('telemetry'));
        };

        CookieConsent.run({
            root: '#main-layout',

            guiOptions: {
                consentModal: {
                    layout: 'box',
                    position: 'bottom right'
                },
                preferencesModal: {
                    layout: 'box'
                }
            },

            categories: {
                necessary: {
                    enabled: true,
                    readOnly: true
                },
                telemetry: {
                    enabled: allowed
                }
            },

            onFirstConsent: report,
            onChange: report,

            language: {
                default: 'flox',
                translations: {
                    flox: {
                        consentModal: {
                            title: texts.title,
                            description: texts.description,
                            acceptAllBtn: texts.accept,
                            acceptNecessaryBtn: texts.reject,
                            showPreferencesBtn: texts.manage
                        },
                        preferencesModal: {
                            title: texts.preferencesTitle,
                            acceptAllBtn: texts.accept,
                            acceptNecessaryBtn: texts.reject,
                            savePreferencesBtn: texts.save,
                            closeIconLabel: texts.close,
                            sections: [
                                {
                                    title: texts.necessaryTitle,
                                    description: texts.necessaryDescription,
                                    linkedCategory: 'necessary'
                                },
                                {
                                    title: texts.telemetryTitle,
                                    description: texts.telemetryDescription,
                                    linkedCategory: 'telemetry'
                                }
                            ]
                        }
                    }
                }
            }
        });

        document.documentElement.classList.add('cc--darkmode');
        this.setTelemetry(allowed);
    },

    // The setting changed elsewhere (the privacy page, another device): the banner follows it
    setTelemetry: function (allowed) {
        if (typeof CookieConsent === 'undefined' || !CookieConsent.validConsent()) {
            return;
        }

        if (CookieConsent.acceptedCategory('telemetry') !== allowed) {
            CookieConsent.acceptCategory(allowed ? ['necessary', 'telemetry'] : ['necessary']);
        }
    }
};

export function init(handler, texts, allowed) {
    return floxConsent.init(handler, texts, allowed);
}

export function setTelemetry(allowed) {
    floxConsent.setTelemetry(allowed);
}
