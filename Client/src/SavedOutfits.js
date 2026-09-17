export const STORAGE_KEY = 'opens4l.character.outfits.v1';
export const LAST_OUTFIT_KEY = 'opens4l.character.last-outfit.v1';

export function loadLastOutfitId(storage) {
  return storage.getItem(LAST_OUTFIT_KEY) || '';
}

export function saveLastOutfitId(storage, id) {
  if (id) storage.setItem(LAST_OUTFIT_KEY, id);
  else storage.removeItem?.(LAST_OUTFIT_KEY);
}

function validOutfit(outfit) {
  return outfit && typeof outfit.id === 'string' && typeof outfit.name === 'string'
    && typeof outfit.bodyId === 'string' && outfit.equipment && typeof outfit.equipment === 'object'
    && !Array.isArray(outfit.equipment) && Object.values(outfit.equipment).every(item =>
      item && typeof item.itemId === 'string' && typeof item.variantId === 'string');
}

export function loadOutfits(storage) {
  const value = storage.getItem(STORAGE_KEY);
  if (value === null) return [];
  let data;
  try { data = JSON.parse(value); } catch { throw new Error('Saved outfits could not be read. Existing data has not been changed.'); }
  if (!data || data.version !== 1 || !Array.isArray(data.outfits) || !data.outfits.every(validOutfit))
    throw new Error('Unsupported saved-outfit data. Existing data has not been changed.');
  return data.outfits;
}

export function saveOutfit(storage, name, character) {
  name = name.trim();
  if (!name || name.length > 80) throw new Error('Enter an outfit name between 1 and 80 characters.');
  const outfits = loadOutfits(storage);
  const previous = outfits.find(outfit => outfit.name.toLowerCase() === name.toLowerCase());
  if (!previous && outfits.length >= 100) throw new Error('The local outfit limit is 100. Delete an outfit before saving another.');
  const outfit = {
    id: previous?.id ?? crypto.randomUUID(), name, bodyId: character.bodyId,
    equipment: Object.fromEntries([...character.equipment].map(([slot, selection]) =>
      [slot, { itemId: selection.item.id, variantId: selection.variant.id }])),
  };
  const updated = previous ? outfits.map(entry => entry.id === previous.id ? outfit : entry) : [...outfits, outfit];
  storage.setItem(STORAGE_KEY, JSON.stringify({ version: 1, outfits: updated }));
  return outfit;
}

export function deleteOutfit(storage, id) {
  const outfits = loadOutfits(storage).filter(outfit => outfit.id !== id);
  storage.setItem(STORAGE_KEY, JSON.stringify({ version: 1, outfits }));
}

/** Validate the complete selection before changing the visible character. */
export function validateOutfit(outfit, catalog) {
  if (!validOutfit(outfit)) throw new Error('Invalid saved outfit.');
  const body = catalog.bodies.find(body => body.id === outfit.bodyId);
  if (!body) throw new Error(`This outfit needs an unimported body: ${outfit.bodyId}`);
  for (const slot of Object.keys(body.defaults)) if (!outfit.equipment[slot]) throw new Error(`Saved outfit is missing required slot: ${slot}`);
  for (const [slot, selection] of Object.entries(outfit.equipment)) {
    const item = body.items.find(item => item.slot === slot && item.id === selection.itemId);
    if (!item) throw new Error(`Saved item ${selection.itemId} is not available in this catalog (${slot}).`);
    if (!item.variants.some(variant => variant.id === selection.variantId))
      throw new Error(`Saved skin ${selection.variantId} is not available for item ${selection.itemId}.`);
  }
  return body;
}
