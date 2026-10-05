import { mount } from 'svelte';
import App from './App.svelte';
import './styles/app.css';

const root = document.getElementById('app');
if (root) mount(App, { target: root });
