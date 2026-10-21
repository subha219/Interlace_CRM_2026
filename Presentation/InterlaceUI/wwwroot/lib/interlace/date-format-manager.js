const DateFormatManager = {
    formatToLocale: (date) => {
        const formatter = new Intl.DateTimeFormat('en-CA', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
        });
        const newDate = new Date(date)
        return formatter.format(newDate);
    },

    preserveClientDate: (date) => {
        if (!date) return null
        const now = new Date()
        const localDate = new Date(date.getFullYear(), date.getMonth(), date.getDate(), now.getHours(), now.getMinutes(), now.getSeconds())
        return localDate.getFullYear() + '-' +
            String(localDate.getMonth() + 1).padStart(2, '0') + '-' +
            String(localDate.getDate()).padStart(2, '0') + 'T' +
            String(localDate.getHours()).padStart(2, '0') + ':' +
            String(localDate.getMinutes()).padStart(2, '0') + ':' +
            String(localDate.getSeconds()).padStart(2, '0')
    },


};