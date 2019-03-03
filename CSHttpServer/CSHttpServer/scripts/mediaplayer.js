var loop = true;
var random = false;

var currentMedia;
var currentText = document.querySelector("#current");
var player = document.querySelector("#player");

player.onerror = playNext;
player.onended = playNext;

function playNext() {
    next();
    player.play();
}

function item_onclick(target) {
    if (target.parentElement === currentMedia)
        return;
    select(target.parentElement);
    player.play();
}

function select(target) {
    var playing = !player.paused;
    if (currentMedia) {
        currentMedia.classList.add("media-item");
        currentMedia.classList.remove("media-item-selected");
    }
    currentMedia = target;
    currentMedia.classList.add("media-item-selected");
    currentMedia.classList.remove("media-item");
    var name = currentMedia.firstElementChild.innerText;
    player.src = name;
    currentText.innerText = name;
    if (playing) player.play();
}

function next() {

    if (random) {
        var index = currentMedia.parentElement.childElementCount;
        index = Math.floor(Math.random() * index);
        var next = currentMedia.parentElement.children[index];
    }
    else {
        var next = currentMedia.nextElementSibling;
        if (!next) {
            if (!loop) return;
            next = currentMedia.parentElement.firstElementChild;
        }
    }
    select(next);
}

function previous() {
    var previous = currentMedia.previousElementSibling;
    if (!previous) {
        if (!loop) return;
        previous = currentMedia.parentElement.lastElementChild;
    }
    select(previous);
}