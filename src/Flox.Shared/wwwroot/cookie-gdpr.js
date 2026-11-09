// The banner is the face of the telemetry consent; the setting GeneralAppSettings.AllowTelemetry stays the source of truth.
// The library's own cookie only remembers that the user has answered
window.floxConsent = {
    init: function (handler, texts, allowed) {
        if (typeof CookieConsent === 'undefined') {
            console.error('CookieConsent library not loaded');
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
