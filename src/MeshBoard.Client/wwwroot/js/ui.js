window.meshboardUi = {
    _lockCount: 0,

    lockScroll() {
        this._lockCount++;
        if (this._lockCount === 1) {
            document.body.style.overflow = 'hidden';
        }
    },

    unlockScroll() {
        this._lockCount = Math.max(0, this._lockCount - 1);
        if (this._lockCount === 0) {
            document.body.style.overflow = '';
        }
    }
};
