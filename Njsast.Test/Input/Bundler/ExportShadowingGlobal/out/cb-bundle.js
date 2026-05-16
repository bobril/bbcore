(() => {
    var Image_image;
    function loadImage() {
        return new Image();
    }
    Image_image = function() {
        function Image() {
            console.log("constructed");
        }
        return Image;
    }();
    URL.createObjectURL("");
    new Image_image();
    loadImage();
})();

