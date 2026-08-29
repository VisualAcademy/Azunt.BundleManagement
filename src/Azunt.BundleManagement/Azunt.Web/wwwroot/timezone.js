window.azuntBundleTimeZone = {
    getLocalOffsetMinutes: function () {
        return -new Date().getTimezoneOffset();
    },

    getBrowserCulture: function () {
        return navigator.language || "en-US";
    }
};
