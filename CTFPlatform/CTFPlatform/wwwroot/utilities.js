let clickCallback = null;

function registerClickTrigger(callbackObject, callbackName, containerName) { 
    clickCallback = {
        object: callbackObject,
        name: callbackName
    };
    
    document.getElementById(containerName).addEventListener('click', triggerClick);
}

function triggerClick(e) {
    if(clickCallback && e.currentTarget === e.target)
        clickCallback.object.invokeMethodAsync(clickCallback.name, e);
}