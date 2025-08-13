<script setup lang="ts">
import { RouterLink, RouterView } from 'vue-router'
import HelloWorld from './components/HelloWorld.vue'
import { useTemplateRef } from 'vue';
import type { ShallowRefMarker } from '@vue/reactivity';



((<any>window).chrome).webview.addEventListener('message', (event:any) => {

});

function sendToDotNet(message:any) {
    ((<any>window).chrome).webview.postMessage({mes:message});
}

import { defineComponent, ref, onMounted } from "vue";
import SurveyTree from './views/SurveyTree.vue';
import PopPage from './views/PopPage.vue';
import TabPage from './views/TabPage.vue';



</script>

<template>
  <TabPage></TabPage>
</template>


<style scoped>
</style>
