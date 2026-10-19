// Dev tool: the game plus three.js's glTF exporter, so the café and the characters can be saved as .glb for Unity.
import '../../client/src/main';
import * as THREE from 'three';
import { GLTFExporter } from 'GLTFExporter';
import { makePerson, PEOPLE } from '../../client/src/people';
import { makeTable, makeTeaGlass } from '../../client/src/ahwa';
(window as unknown as { __export: unknown }).__export = { THREE, GLTFExporter, makePerson, PEOPLE, makeTable, makeTeaGlass };
