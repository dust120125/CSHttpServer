var loop = true;
var random = false;

var currentMedia;
var currentText = document.querySelector("#current");
var player = document.querySelector("#player");
var mediaList = document.querySelector("#media-list");

var previousOne;
var previousList;
var nextList;

var path_media_Table = [];
var pathIndexer = [];

player.onerror = playNext;
player.onended = playNext;

player.onplaying = function () {
    if (player.videoHeight == 0) {
        player.height = 60;
        player.style.width = "100%";
    } else {
        player.height = player.videoHeight;
        player.style.width = "";
    }
}

function setRandom(value) {
    random = value;
    if (random) {
        previousList = [];
        nextList = [];
        if (previousOne) previousList.push(previousOne);
    }
}

function playNext() {
    if (mediaList.childElementCount == 0) return;
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
    var pathIndex = currentMedia.firstElementChild.pathIndex;
    var path = pathIndexer[pathIndex];
    player.src = "/" + dirScriptPath + "/" + path + name;
    currentText.innerText = name;
    player.play();
    player.pause();
    if (playing) player.play();
}

function getRandom() {
    var result, index;
    do {
        index = mediaList.childElementCount;
        index = Math.floor(Math.random() * index);
        result = mediaList.children[index];
    } while (result == currentMedia)
    return result;
}

function next() {
    if (mediaList.childElementCount == 0) resetPlayer();
    if (random) {
        if (nextList.length > 0) {
            var next = nextList.pop();
        }
        else {
            var next = getRandom();
        }
    }
    else {
        var next = currentMedia.nextElementSibling;
        if (!next) {
            if (!loop) return;
            next = mediaList.firstElementChild;
        }
    }
    previousOne = currentMedia;
    if (random) {
        previousList.push(previousOne);
        if (previousList.length > 20) {
            previousList.shift();
        }
    }
    select(next);
}

function previous() {
    if (mediaList.childElementCount == 0) resetPlayer();
    if (random) {
        var previous = previousList.pop();
        if (!previous) previous = getRandom();
    }
    else {
        var previous = currentMedia.previousElementSibling;
        if (!previous) {
            if (!loop) return;
            previous = mediaList.lastElementChild;
        }
    }
    if (random) {
        nextList.push(currentMedia);
        if (nextList.length > 20) {
            nextList.shift();
        }
    }
    select(previous);
}

function resetPlayer() {
    player.pause();
    player.src = null;
    currentMedia = null;
    previousOne = null;
    previousList = null;
    nextList = null;
    currentText.innerText = "None";
}

function batchAddMedia(media) {
    if (!media.path) return;
    var index = pathIndexer.indexOf(media.path);
    if (index == -1) {
        index = pathIndexer.length;
        pathIndexer.push(media.path);
        path_media_Table.push([]);
    }

    for (var k in media.items) {
        var name = media.items[k];
        if (path_media_Table[index].includes(name)) continue;
        path_media_Table[index].push(name);
        var item = createMediaItem(index, name);
        mediaList.appendChild(item);
    }
}

function addMedia(path, name) {
    var index = pathIndexer.indexOf(path);
    if (index == -1) {
        index = pathIndexer.length;
        pathIndexer.push(path);
        path_media_Table.push([]);
    }
    if (path_media_Table[index].includes(name)) return;
    path_media_Table[index].push(name);
    var item = createMediaItem(index, name);
    mediaList.appendChild(item);
}

function createMediaItem(pathIndex, name) {
    var item = document.createElement("div");
    item.classList.add("media-item");
    var span = document.createElement("span");
    span.setAttribute("onclick", "item_onclick(this)");
    span.innerText = name;
    span.pathIndex = pathIndex;
    var delb = document.createElement("input");
    delb.classList.add("delete-button");
    delb.type = "button";
    delb.value = "✘";
    delb.onclick = function () { removeItem(this); };
    item.appendChild(span);
    item.appendChild(delb);
    return item;
}

function removeItem(item) {
    var div = item.parentElement;
    if (div == currentMedia) {
        if (mediaList.childElementCount > 1) next();
        else resetPlayer();
    }
    mediaList.removeChild(div);

    var span = div.firstElementChild;
    var name = span.innerText;
    var index = span.pathIndex;
    var ta = path_media_Table[index];
    var i = ta.indexOf(name);
    if (i > -1) ta.splice(i, 1);
}